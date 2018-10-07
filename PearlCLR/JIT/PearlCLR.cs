using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using LLVMSharp;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using NLog;

namespace PearlCLR.JIT
{
    public class PearlCLR
    {
        public PearlCLR(string file, string target = null)
        {
            assembly = AssemblyDefinition.ReadAssembly(file);
            LLVM.InitializeX86TargetMC();
            LLVM.InitializeX86Target();
            LLVM.InitializeX86TargetInfo();
            LLVM.InitializeX86AsmParser();
            LLVM.InitializeX86AsmPrinter();

            _context = new JITContext
            {
                CLR = this,
                ModuleRef = LLVM.ModuleCreateWithName("PearlCLRModule"),
                CLRLogger = LogManager.GetCurrentClassLogger()
            };
            _context.ContextRef = LLVM.GetModuleContext(_context.ModuleRef);
            _context.TypeResolver = new LLVMTypeResolver(_context);
        }

        /// <summary>
        ///     This assume we have the BCL aka System namespace library
        /// </summary>
        private AssemblyDefinition assembly { get; }

        private JITCompilerOptions _options { get; } = new JITCompilerOptions();
        private JITContext _context { get; }
        private List<OpcodeHandlerModule> OpcodeHandlers = new List<OpcodeHandlerModule>();

        private void LoadOpcodeHandlerModules()
        {
            OpcodeHandlers.Add(new MetaOpcodeModule(_context));
            OpcodeHandlers.Add(new ConstantOpcodeModule(_context));
            OpcodeHandlers.Add(new ParameterOpcodeModule(_context));
            OpcodeHandlers.Add(new LocalVariableOpcodeModule(_context));
            OpcodeHandlers.Add(new ConversionOpcodeModule(_context));
            OpcodeHandlers.Add(new ArithmeticOpcodeModule(_context));
            OpcodeHandlers.Add(new ComparsionOpcodeModule(_context));
        }

        private void AddBCLObjects()
        {
            var structDef = new StructDefinition();

            if (_options.MetadataTypeHandlingModeOption == MetadataTypeHandlingMode.Full_Fixed)
            {
                // C# Fields are going to be seen as Int32, but it may not necessarily be 32 bit integer,
                // What is important is the specified type information in LLVM.
                // This is intended to allow a more flexible optimization feature.
                structDef.CS_FieldDefs = new[]
                {
                    new FieldDefinition("___INTERNAL__DO_NOT_TOUCH__CLR__TYPEHANDLE",
                        FieldAttributes.SpecialName | FieldAttributes.Private | FieldAttributes.CompilerControlled,
                        MiniBCL.Int32Type),
                    new FieldDefinition("___INTERNAL__DO_NOT_TOUCH__CLR__SYNC",
                        FieldAttributes.SpecialName | FieldAttributes.Private | FieldAttributes.CompilerControlled,
                        MiniBCL.Int32Type)
                };

                structDef.CS_StructName = "Object";
                structDef.LL_StructName = "Object";

                structDef.LL_FieldTypeRefs = new[]
                {
                    new LLVMFieldDefAndRef(MiniBCL.Int32Type, LLVM.IntType(_options.MetadataFixedLength)),
                    new LLVMFieldDefAndRef(MiniBCL.Int32Type, LLVM.IntType(_options.MetadataFixedLength))
                };

                structDef.StructTypeRef =
                    LLVM.StructType(structDef.LL_FieldTypeRefs.Select(I => I.FieldTypeRef).ToArray(), true);

                _context.FullSymbolToTypeRef.Add("System.Object", structDef);

                // TODO: Add actual Type retrieval for objects
                _context.SymbolToCallableFunction.Add("System.Object::GetType", default(LLVMValueRef));
            }
            else
            {
                // TODO: Implement support for Full_Native and None.
                throw new NotImplementedException();
            }
        }

        public void ProcessMainModule()
        {
            void ProcessWork(string msg,  params Action[] works)
            {
                _context.CLRLogger.Debug($"Started: {msg}");
                foreach (var work in works)
                    work();
                _context.CLRLogger.Debug($"Completed: {msg}");
            }
            LLVM.InstallFatalErrorHandler(reason => _context.CLRLogger.Debug(reason));
            LoadDependency();
            _context.CLRLogger.Info("Running Process Main Module");
            ProcessWork("Adding Critical Objects", AddBCLObjects);
            ProcessWork("Process all exported types", ProcessAllExportedTypes);
            ProcessWork("Process all functions", LoadOpcodeHandlerModules, () => ProcessFunction(assembly.MainModule.EntryPoint));
            ProcessWork("Link in JIT", LinkJIT);
            ProcessWork("Verify LLVM emitted codes", Verify);
            ProcessWork("Optimize LLVM emitted codes", Optimize);
            ProcessWork("Compile LLVM emitted codes", Compile);
            ProcessWork("Print emitted LLVM IR", () => PrintToLLVMIR("MainModule.bc"));
            ProcessWork("Run entry function", RunEntryFunction);
        }

        private void Verify()
        {
            LLVM.DumpModule(_context.ModuleRef);
            var passManager = LLVM.CreatePassManager();
            LLVM.AddVerifierPass(passManager);
            LLVM.RunPassManager(passManager, _context.ModuleRef);
        }


        internal void LoadDependency()
        {
            switch (Environment.OSVersion.Platform)
            {
                // We need printf!
                case PlatformID.Unix when LLVM.LoadLibraryPermanently("/usr/lib/libc.so.6") == new LLVMBool(1):
                    throw new Exception("Failed to load Libc!");
                case PlatformID.Win32NT when LLVM.LoadLibraryPermanently("msvcrt.dll") == new LLVMBool(1):
                    throw new Exception("Failed to load msvcrt!");
            }

            _context.CLRLogger.Info("Successfully loaded LibC Library.");
            var ptr = LLVM.SearchForAddressOfSymbol("printf");
            if (ptr == IntPtr.Zero)
                throw new Exception("Can't find Printf!");
            var printfType = LLVM.FunctionType(LLVM.Int32Type(), new[] {LLVM.PointerType(LLVM.Int8Type(), 0)}, true);
            _context.SymbolToCallableFunction.Add("System.Console::WriteLine",
                LLVM.AddFunction(_context.ModuleRef, "printf", printfType));
        }

        public void PrintToLLVMIR(string filename)
        {
            LLVM.WriteBitcodeToFile(_context.ModuleRef, filename);
            _context.CLRLogger.Info("LLVM Bitcode written to {0}", filename);
        }

        internal void ProcessAllExportedTypes()
        {
            foreach (var exportedType in assembly.MainModule.GetTypes())
            {
                if (_context.FullSymbolToTypeRef.ContainsKey(exportedType.FullName))
                    continue;
                // Handle for Struct
                var structDef = _context.TypeResolver.ProcessForStruct(exportedType);

                _context.FullSymbolToTypeRef.Add(exportedType.FullName, structDef);
            }
        }

        internal void ProcessFunction(MethodDefinition method, bool isMain = true)
        {
            _context.CLRLogger.Debug($"{method.FullName}");
            var funcContext = new FunctionContext {MethodDef = method, isMain = isMain};

            if (method.HasThis)
                funcContext.FunctionType = LLVM.FunctionType(
                    _context.TypeResolver.ResolveType(method.ReturnType).FieldTypeRef,
                    method.Parameters.Select(p => _context.TypeResolver.ResolveType(p.ParameterType).FieldTypeRef)
                        .Prepend(LLVM.PointerType(
                            _context.FullSymbolToTypeRef[method.DeclaringType.FullName].StructTypeRef, 0)).ToArray(),
                    false
                );
            else
                funcContext.FunctionType = LLVM.FunctionType(
                    _context.TypeResolver.ResolveType(method.ReturnType).FieldTypeRef,
                    method.Parameters.Select(p => _context.TypeResolver.ResolveType(p.ParameterType).FieldTypeRef)
                        .ToArray(),
                    false
                );

            var entryFunctionSymbol = SymbolHelper.GetCSToLLVMSymbolName(method);

            if (method.IsPInvokeImpl)
            {
                funcContext.CurrentBlockRef = LLVM.AddFunction(_context.ModuleRef,
                    method.PInvokeInfo.EntryPoint ?? entryFunctionSymbol,
                    funcContext.FunctionType);
                if (method.PInvokeInfo.Module.Name != "__internal")
                    throw new NotSupportedException("Does not support actual P/Invoke just yet.");

                _context.SymbolToCallableFunction.Add(SymbolHelper.GetCSToLLVMSymbolName(method), funcContext.CurrentBlockRef);
                return;
            }

            funcContext.FunctionRef = LLVM.AddFunction(_context.ModuleRef, isMain ? "main" : entryFunctionSymbol, funcContext.FunctionType);
            funcContext.CurrentBlockRef = LLVM.AppendBasicBlock(funcContext.FunctionRef, "entry");
            funcContext.Builder = LLVM.CreateBuilder();
            
            LLVM.PositionBuilderAtEnd(funcContext.Builder, funcContext.CurrentBlockRef);
            
            foreach (var local in method.Body.Variables)
            {
                LLVMValueRef alloca;
                _context.CLRLogger.Debug(local.VariableType.FullName);
                funcContext.LocalVariableTypes.Add(local.VariableType);
                if (local.VariableType.IsPointer)
                {
                    alloca = LLVM.BuildAlloca(funcContext.Builder, _context.TypeResolver.Resolve(local.VariableType),
                        $"Alloca_{local.VariableType.Name}");
                }
                else if (local.VariableType.IsPrimitive)
                {
                    var type = _context.TypeResolver.Resolve(local.VariableType);
                    alloca = LLVM.BuildAlloca(funcContext.Builder, type, $"Alloca_{local.VariableType.Name}");
                }
                else
                {
                    if (!_context.FullSymbolToTypeRef.ContainsKey(local.VariableType.FullName))
                        _context.FullSymbolToTypeRef.Add(local.VariableType.FullName,
                            _context.TypeResolver.ProcessForStruct(local.VariableType.Resolve()));
                    alloca = LLVM.BuildAlloca(funcContext.Builder,
                        LLVM.PointerType(_context.FullSymbolToTypeRef[local.VariableType.FullName].StructTypeRef, 0),
                        $"Alloca_{local.VariableType.MakePointerType().Name}");
                }
                _context.CLRLogger.Debug(
                    $"Local variable defined: {local.VariableType.Name} - {alloca} with Type Of {alloca.TypeOf()}");
                funcContext.LocalVariables.Add(alloca);
            }

            // Handle Branch Blocks
            foreach (var instruction in method.Body.Instructions)
                if (instruction.OpCode == OpCodes.Br_S ||
                    instruction.OpCode == OpCodes.Br ||
                    instruction.OpCode == OpCodes.Brtrue ||
                    instruction.OpCode == OpCodes.Brfalse ||
                    instruction.OpCode == OpCodes.Brtrue_S ||
                    instruction.OpCode == OpCodes.Brfalse_S ||
                    instruction.OpCode == OpCodes.Beq ||
                    instruction.OpCode == OpCodes.Beq_S ||
                    instruction.OpCode == OpCodes.Bge ||
                    instruction.OpCode == OpCodes.Bge_S ||
                    instruction.OpCode == OpCodes.Bge_Un ||
                    instruction.OpCode == OpCodes.Bge_Un_S ||
                    instruction.OpCode == OpCodes.Bgt ||
                    instruction.OpCode == OpCodes.Bgt_S ||
                    instruction.OpCode == OpCodes.Bgt_Un ||
                    instruction.OpCode == OpCodes.Bgt_Un_S ||
                    instruction.OpCode == OpCodes.Ble ||
                    instruction.OpCode == OpCodes.Ble_S ||
                    instruction.OpCode == OpCodes.Ble_Un ||
                    instruction.OpCode == OpCodes.Ble_Un_S ||
                    instruction.OpCode == OpCodes.Blt ||
                    instruction.OpCode == OpCodes.Blt_S ||
                    instruction.OpCode == OpCodes.Blt_Un ||
                    instruction.OpCode == OpCodes.Blt_Un_S ||
                    instruction.OpCode == OpCodes.Bne_Un ||
                    instruction.OpCode == OpCodes.Bne_Un_S)
                {
                    var operand = (Instruction) instruction.Operand;
                    var branch = LLVM.AppendBasicBlock(funcContext.FunctionRef, "branch");
                    funcContext.BranchTo.TryAdd(operand, branch);
                    funcContext.BranchToProcess.Push(new KeyValuePair<Instruction, LLVMBasicBlockRef>(operand, branch));
                    _context.CLRLogger.Debug($"Branch to {operand}");
                }

            void ProcessInstructions(Instruction paramInstruction)
            {
                var isFirst = true;
                var instruction = paramInstruction;
                do
                {
                    var result = new BuildResult(false);
                    if (isFirst)
                        isFirst = false;
                    else
                        instruction = instruction.Next;

                    foreach (var handler in OpcodeHandlers)
                    {
                        result = handler.Build(instruction, funcContext);
                        if (result.BreakLoop)
                        {
                            goto BreakLoop;
                        }

                        if (result.Success)
                            break;
                    }
                    
                    if (result.Success)
                    {
                        continue;
                    }

                    _context.CLRLogger.Debug($"Unhandled Opcode: {instruction.OpCode.ToString()}");
                    throw new Exception($"Unhandled Opcode: {instruction.OpCode.ToString()}");
                } while (instruction.Next != null);
                BreakLoop:;
            }

            var first = method.Body.Instructions.First();
            ProcessInstructions(first);
            funcContext.ProcessedBranch.Add(first);
            while (funcContext.BranchToProcess.Count > 0)
            {
                var item = funcContext.BranchToProcess.Pop();
                var terminator = item.Value.GetBasicBlockTerminator();
                if (terminator.Pointer != IntPtr.Zero)
                    continue;
                LLVM.PositionBuilderAtEnd(funcContext.Builder, item.Value);
                funcContext.CurrentBlockRef = item.Value;
                ProcessInstructions(item.Key);
                funcContext.ProcessedBranch.Add(item.Key);
            }

            funcContext.CurrentBlockRef.Dump();
            if (LLVM.VerifyFunction(funcContext.FunctionRef, LLVMVerifierFailureAction.LLVMPrintMessageAction) != new LLVMBool(0))
                throw new Exception("Function is not well formed!");

            _context.SymbolToCallableFunction.Add(entryFunctionSymbol, funcContext.FunctionRef);
        }

        private void LinkJIT()
        {
            _context.CLRLogger.Debug(
                LLVM.VerifyModule(_context.ModuleRef, LLVMVerifierFailureAction.LLVMPrintMessageAction,
                    out var error) !=
                new LLVMBool(0)
                    ? $"Error: {error}"
                    : "Successfully verified the module!");
            LLVM.LinkInMCJIT();
        }

        private void Optimize()
        {
            var pass = LLVM.CreatePassManager();
            LLVM.AddAlwaysInlinerPass(pass);
            LLVM.AddFunctionInliningPass(pass);
            LLVM.AddDeadArgEliminationPass(pass);
            LLVM.AddDeadStoreEliminationPass(pass);
            LLVM.AddInstructionCombiningPass(pass);
            LLVM.AddInternalizePass(pass, 1);
            LLVM.AddStripSymbolsPass(pass);
            LLVM.AddStripDeadPrototypesPass(pass);
            LLVM.AddAggressiveDCEPass(pass);
            LLVM.AddGlobalDCEPass(pass);
            LLVM.AddLoopDeletionPass(pass);
            LLVM.AddLoopIdiomPass(pass);
            LLVM.AddLoopRerollPass(pass);
            LLVM.AddLoopUnrollPass(pass);

            for (var passes = 0; passes < 10; ++passes)
                LLVM.RunPassManager(pass, _context.ModuleRef);
            LLVM.DumpModule(_context.ModuleRef);
        }

        private void Compile()
        {
            var options = new LLVMMCJITCompilerOptions { NoFramePointerElim = 1, OptLevel = 3, CodeModel = LLVMCodeModel.LLVMCodeModelLarge};
            LLVM.InitializeMCJITCompilerOptions(options);
            if (LLVM.CreateMCJITCompilerForModule(out var compiler, _context.ModuleRef, options, out var error) !=
                new LLVMBool(0))
            {
                _context.CLRLogger.Debug($"Error: {error}");
                throw new Exception(error);
            }
            _context.EngineRef = compiler;
        }

        private void RunEntryFunction()
        {
            var entrySymbol = SymbolHelper.GetCSToLLVMSymbolName(assembly.MainModule.EntryPoint);
            var symbol = _context.SymbolToCallableFunction[entrySymbol];
            var funcPtr =
                LLVM.GetPointerToGlobal(_context.EngineRef, symbol);
            var mainFunc = (EntryFunc_dt)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(EntryFunc_dt));
            mainFunc();
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void EntryFunc_dt();
    }
}