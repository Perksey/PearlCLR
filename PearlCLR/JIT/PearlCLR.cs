using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Mono.Cecil;
using LLVMSharp;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using NLog;
using OpCodes = Mono.Cecil.Cil.OpCodes;

namespace PearlCLR.JIT
{
    public class PearlCLR
    {
        /// <summary>
        /// This assume we have the BCL aka System namespace library
        /// </summary>
        private AssemblyDefinition assembly { get; }

        private Logger clrLogger { get; set; }
        private readonly LLVMModuleRef llvmModuleRef;
        private readonly LLVMContextRef llvmContextRef;
        private LLVMExecutionEngineRef llvmEngineRef;
        private Dictionary<string, StructDefinition> FullSymbolToTypeRef { get; }
        private Dictionary<string, LLVMValueRef> SymbolToCallableFunction { get; }
        private Dictionary<string, LLVMTypeRef> SymbolToFunctionPointerProto { get; }
        private JITCompilerOptions _options { get; } = new JITCompilerOptions();
        public PearlCLR(string file)
        {
            assembly = AssemblyDefinition.ReadAssembly(file);
            LLVM.InitializeX86TargetMC();
            LLVM.InitializeX86Target();
            LLVM.InitializeX86TargetInfo();
            LLVM.InitializeX86AsmParser();
            LLVM.InitializeX86AsmPrinter();

            FullSymbolToTypeRef = new Dictionary<string, StructDefinition>();
            SymbolToCallableFunction = new Dictionary<string, LLVMValueRef>();
            SymbolToFunctionPointerProto = new Dictionary<string, LLVMTypeRef>();

            llvmModuleRef = LLVM.ModuleCreateWithName("PearlCLRModule");
            llvmContextRef = LLVM.GetModuleContext(llvmModuleRef);

            clrLogger = LogManager.GetCurrentClassLogger();
        }

        private void AddBCLObjects()
        {
            StructDefinition structDef = new StructDefinition();

            if (_options.MetadataTypeHandlingModeOption == MetadataTypeHandlingMode.Full_Fixed)
            {
                // C# Fields are going to be seen as Int32, but it may not necessarily be 32 bit integer,
                // What is important is the specified type information in LLVM.
                // This is intended to allow a more flexible optimization feature.
                structDef.CS_FieldDefs = new [] {
                    new FieldDefinition("___INTERNAL__DO_NOT_TOUCH__CLR__TYPEHANDLE",
                    FieldAttributes.SpecialName | FieldAttributes.Private | FieldAttributes.CompilerControlled,
                    MiniBCL.Int32Type), new FieldDefinition("___INTERNAL__DO_NOT_TOUCH__CLR__SYNC",
                    FieldAttributes.SpecialName | FieldAttributes.Private | FieldAttributes.CompilerControlled,
                    MiniBCL.Int32Type)};

                structDef.CS_StructName = "Object";
                structDef.LL_StructName = "Object";

                structDef.LL_FieldTypeRefs = new[]
                {
                    new LLVMFieldDefAndRef(MiniBCL.Int32Type, LLVM.IntType(_options.MetadataFixedLength)),
                    new LLVMFieldDefAndRef(MiniBCL.Int32Type, LLVM.IntType(_options.MetadataFixedLength))
                };

                structDef.StructTypeRef = LLVM.StructType(structDef.LL_FieldTypeRefs.Select(I => I.FieldTypeRef).ToArray(), true);
                
                FullSymbolToTypeRef.Add("System.Object", structDef);
                
                SymbolToCallableFunction.Add("System.Object::GetType", default(LLVMValueRef));
                
                clrLogger.Debug("Added");
            }
        }

        public void ProcessMainModule()
        {
            LLVM.InstallFatalErrorHandler(reason => clrLogger.Debug(reason));
            LoadDependency();
            clrLogger.Info("Running Process Main Module");
            clrLogger.Debug("Adding Critical Objects");
            AddBCLObjects();
            clrLogger.Debug("Added Critical Objects");
            clrLogger.Debug("Processing all exported types");
            ProcessAllExportedTypes();
            clrLogger.Debug("Processed all exported types");
            clrLogger.Debug("Processing all functions");
            ProcessFunction(assembly.MainModule.EntryPoint);
            clrLogger.Debug("Processed all functions");
            clrLogger.Debug("Linking JIT");
            LinkJIT();
            clrLogger.Debug("Linked JIT");
            clrLogger.Debug("Verifying LLVM emitted codes");
            Verify();
            clrLogger.Debug("Verified LLVM emitted codes");
            clrLogger.Debug("Optimizing LLVM emitted codes");
            //Optimize();
            clrLogger.Debug("Optimized LLVM emitted codes");
            clrLogger.Debug("Compiling LLVM emitted codes");
            Compile();
            clrLogger.Debug("Compiled LLVM emitted codes");
            clrLogger.Debug("Printing LLVM IR from Emitted LLVM Emitted Codes");
            PrintToLLVMIR("MainModule.bc");
            clrLogger.Debug("Printed LLVM IR from Emitted LLVM Emitted Codes");
            clrLogger.Debug("Running Entry Function of Compiled LLVM Emitted Codes");
            RunEntryFunction();
            clrLogger.Debug("Entry Function of Compiled LLVM Emitted Codes concluded.");
        }

        private void Verify()
        {
            LLVM.DumpModule(llvmModuleRef);
            var passManager = LLVM.CreatePassManager();
            LLVM.AddVerifierPass(passManager);
            LLVM.RunPassManager(passManager, llvmModuleRef);
        }


        private void LoadDependency()
        {
            // We need printf!
            if (LLVM.LoadLibraryPermanently("/usr/lib/libc.so.6") == new LLVMBool(1))
            {
                throw new Exception("Failed to load Libc!");
            }

            clrLogger.Info("Successfully loaded LibC Library.");
            var ptr = LLVM.SearchForAddressOfSymbol("printf");
            if (ptr == IntPtr.Zero)
                throw new Exception("Can't find Printf!");
            var printfType = LLVM.FunctionType(LLVM.Int32Type(), new[] {LLVM.PointerType(LLVM.Int8Type(), 0)}, true);
            SymbolToCallableFunction.Add("System.Console::WriteLine",
                LLVM.AddFunction(llvmModuleRef, "printf", printfType));
        }

        private LLVMValueRef DebugPrint(LLVMBuilderRef builder, string format, params LLVMValueRef[] values)
        {
            return LLVM.BuildCall(builder, SymbolToCallableFunction["System.Console::WriteLine"], values.Prepend(LLVM.BuildGlobalStringPtr(builder, format, "")).ToArray(), "");
        }

        private void PrintToLLVMIR(string filename)
        {
            LLVM.WriteBitcodeToFile(llvmModuleRef, filename);
            clrLogger.Info("LLVM Bitcode written to {0}", filename);
        }

        private void ProcessAllExportedTypes()
        {
            foreach (var exportedType in assembly.MainModule.GetTypes())
            {
                if (FullSymbolToTypeRef.ContainsKey(exportedType.FullName))
                    continue;
                // Handle for Struct
                var structDef = ProcessForStruct(exportedType);

                FullSymbolToTypeRef.Add(exportedType.FullName, structDef);
            }
        }

        private FieldDefinition[] ResolveAllFields(TypeDefinition type)
        {
            var Fields = new Stack<FieldDefinition>();
            foreach (var field in type.Fields)
            {
                Fields.Push(field);
            }

            if (type.BaseType != null && type.FullName != type.BaseType.FullName)
            {
                if (type.BaseType.FullName == "System.Object")
                {
                    
                }
                var declaredTypeFields = ResolveAllFields(type.BaseType.Resolve());
                foreach (var field in declaredTypeFields)
                {
                    Fields.Push(field);
                }
            }

            return Fields.ToArray();
        }

        private StructDefinition ProcessForStruct(TypeDefinition type)
        {
            clrLogger.Debug($"\tProcessing {type.FullName} type for LLVM");
            var processed = ResolveAllFields(type.Resolve());
            foreach (var field in processed)
            {
                if (field.FieldType.IsNested && !FullSymbolToTypeRef.ContainsKey(field.FieldType.FullName))
                {
                    FullSymbolToTypeRef.Add(field.FieldType.FullName, ProcessForStruct(field.FieldType.Resolve()));
                }
            }

            var structDef = new StructDefinition
            {
                CS_StructName = type.FullName,
                LL_StructName = type.FullName,
                CS_FieldDefs = processed,
                LL_FieldTypeRefs = processed.Select(I => ResolveType(I.FieldType)).ToArray()
            };
            structDef.StructTypeRef = LLVM.StructTypeInContext(llvmContextRef, structDef.LL_FieldTypeRefs.Select(
                f =>
                {
                    clrLogger.Debug($"\t\t{f.StackType.FullName} - {f.FieldTypeRef}");
                    return f.FieldTypeRef;
                }).ToArray(), true);
            return structDef;
        }

        private LLVMFieldDefAndRef ResolveType(TypeReference fieldType) =>
            new LLVMFieldDefAndRef(fieldType, ResolveLLVMType(fieldType));

        private LLVMTypeRef ResolveLLVMType(TypeReference def)
        {
            if (def.IsPointer)
            {
                var definedType = ResolveLLVMType(def.Resolve());
                for (var I = def.Name.Length - 1; I > -1; --I)
                    if (def.Name[I] == '*')
                        definedType = LLVM.PointerType(definedType, 0);
                    else
                        break;
                return definedType;
            }

            if (def.FullName == "System.Void")
            {
                return LLVM.VoidType();
            }

            if (def.IsPrimitive)
                switch (def.FullName)
                {
                    case "System.Boolean":
                    {
                        return LLVM.Int8Type();
                    }
                    case "System.SByte":
                    {
                        return LLVM.Int8Type();
                    }
                    case "System.Int8":
                    {
                        return LLVM.Int8Type();
                    }
                    case "System.Int16":
                    {
                        return LLVM.Int16Type();
                    }
                    case "System.Int32":
                    {
                        return LLVM.Int32Type();
                    }
                    case "System.Int64":
                    {
                        return LLVM.Int64Type();
                    }
                    case "System.Byte":
                    {
                        return LLVM.Int8Type();
                    }
                    case "System.UInt8":
                    {
                        return LLVM.Int8Type();
                    }
                    case "System.UInt16":
                    {
                        return LLVM.Int16Type();
                    }
                    case "System.UInt32":
                    {
                        return LLVM.Int32Type();
                    }
                    case "System.UInt64":
                    {
                        return LLVM.Int64Type();
                    }
                    case "System.Float":
                    {
                        return LLVM.FloatType();
                    }
                    case "System.Double":
                    {
                        return LLVM.DoubleType();
                    }
                    case "System.Decimal":
                    {
                        LLVM.VectorType()
                        return LLVM.FP128Type();
                    }
                    default:
                    {
                        throw new Exception("Unhandled Type for Primitive!" + def.FullName);
                    }
                }

            if (def.FullName == "System.String")
            {
                if (_options.CStringMode)
                // This is a special case, all string in this CLR is null terminated.
                    return LLVM.PointerType(LLVM.Int8Type(), 0);
            }

            if (def.FullName == "System.Type")
            {
                return LLVM.VoidType();
            }
            if (FullSymbolToTypeRef.TryGetValue(def.FullName, out var val))
            {
                if (def.Resolve().IsClass)
                    return LLVM.PointerType(val.StructTypeRef, 0);
                return val.StructTypeRef;
            }

            throw new NotSupportedException($"Unhandled Type. {def.FullName} {def.DeclaringType}");
        }

        private void ProcessCall(Instruction instruction, LLVMBuilderRef builder, int indent, Stack<BuilderStackItem> builderStack)
        {
            var prepend = string.Empty;
            if (indent > 0)
                for (var I = 0; I < indent; ++I)
                    prepend += "\t";

            void Debug(string msg)
            {
                clrLogger.Debug(prepend + msg);
            }

            var methodToCall = (MethodReference) instruction.Operand;
            Debug(methodToCall.ToString());
            var resolvedMethodToCall = methodToCall.Resolve();
            if (methodToCall.HasThis &&
                resolvedMethodToCall.DeclaringType != null &&
                resolvedMethodToCall.DeclaringType.BaseType != null &&
                (resolvedMethodToCall.DeclaringType.BaseType.FullName == "System.Delegate" ||
                 resolvedMethodToCall.DeclaringType.BaseType.FullName == "System.MulticastDelegate"))
            {
                var refs = new LLVMValueRef[methodToCall.Parameters.Count];
                if (refs.Length > 0)
                    for (var i = methodToCall.Parameters.Count - 1; i > -1; --i)
                    {
                        refs[i] = builderStack.Pop().ValRef.Value;
                    }

                var reference = builderStack.Pop();
                var stackItem = new BuilderStackItem(methodToCall.ReturnType.Resolve(),
                    LLVM.BuildCall(builder, reference.ValRef.Value, refs, ""));
                // We know this is a delegate, now to call it!
                builderStack.Push(stackItem);
                Debug(
                    $"[{instruction.OpCode.Name} {methodToCall.FullName}] -> Determined as Delegate, Popped Reference {reference} and Push {stackItem.ValRef} to Stack");
                return;
            }
            else
            {
                var refs = new LLVMValueRef[methodToCall.HasThis
                    ? methodToCall.Parameters.Count + 1
                    : methodToCall.Parameters.Count];
                var symbol = GetCSToLLVMSymbolName(methodToCall);

                if (refs.Length > (methodToCall.HasThis ? 1 : 0))
                    for (var i = methodToCall.HasThis
                            ? methodToCall.Parameters.Count
                            : methodToCall.Parameters.Count - 1;
                        i > (methodToCall.HasThis ? 0 : -1);
                        --i)
                    {
                        refs[i] = builderStack.Pop().ValRef.Value;
                    }

                if (!SymbolToCallableFunction.ContainsKey(GetCSToLLVMSymbolName(methodToCall)))
                {
                    Debug("Resolving Function");
                    ProcessFunction(methodToCall.Resolve(), false, indent + 1);
                }

                if (methodToCall.HasThis)
                {
                    var reference = builderStack.Pop();
                    if (methodToCall.DeclaringType.FullName != reference.Type.FullName)
                    {
                        refs[0] = LLVM.BuildPointerCast(builder, reference.ValRef.Value,
                            SymbolToCallableFunction[symbol].GetFirstParam().TypeOf(), "");
                    }
                    else
                    {
                        refs[0] = reference.ValRef.Value;
                    }
                }

                if (methodToCall.ReturnType.FullName != "System.Void")
                {
                    var stackItem = new BuilderStackItem(methodToCall.ReturnType.Resolve(),
                        LLVM.BuildCall(builder,
                            SymbolToCallableFunction[GetCSToLLVMSymbolName(methodToCall)],
                            refs, ""));
                    builderStack.Push(stackItem);
                    Debug($"[{instruction.OpCode.Name} {methodToCall.FullName}] -> Push {stackItem.ValRef} to Stack");
                    return;
                }

                var call = LLVM.BuildCall(builder,
                    SymbolToCallableFunction[GetCSToLLVMSymbolName(methodToCall)],
                    refs, "");
                Debug($"[{instruction.OpCode.Name} {methodToCall.FullName}] -> Called {call}");
            }
        }

        LLVMValueRef AutoCast(LLVMBuilderRef builder, LLVMValueRef val, LLVMTypeRef type)
        {
            // Int to Int
            if (val.TypeOf().TypeKind == LLVMTypeKind.LLVMIntegerTypeKind &&
                type.TypeKind == LLVMTypeKind.LLVMIntegerTypeKind)
            {
                return LLVM.BuildZExt(builder, val, type, "");
            }
            
            // Int to Floating Point
            if (val.TypeOf().TypeKind == LLVMTypeKind.LLVMIntegerTypeKind &&
                type.TypeKind == LLVMTypeKind.LLVMFloatTypeKind)
            {
                return LLVM.BuildSIToFP(builder, val, type, "");
            }
            
            // Floating Point to Int
            if (val.TypeOf().TypeKind == LLVMTypeKind.LLVMIntegerTypeKind &&
                type.TypeKind == LLVMTypeKind.LLVMIntegerTypeKind)
            {
                return LLVM.BuildFPToSI(builder, val, type, "");
            }
            
            // Floating Point to Floating Point
            if (val.TypeOf().TypeKind == LLVMTypeKind.LLVMIntegerTypeKind &&
                type.TypeKind == LLVMTypeKind.LLVMFloatTypeKind)
            {
                return LLVM.BuildFPCast(builder, val, type, "");
            }
            throw new Exception($"{val.TypeOf().TypeKind} to {type.TypeKind}");
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void EntryFunc_dt();

        private void ProcessFunction(MethodDefinition method, bool isMain = true, int indent = 0)
        {
            var prepend = string.Empty;
            if (indent > 0)
                for (var I = 0; I < indent; ++I)
                    prepend += "\t";

            void Debug(string msg)
            {
                clrLogger.Debug(prepend + msg);
            }
            
            Debug($"{method.FullName}");
            var builderStack = new Stack<BuilderStackItem>();
            var localVariables = new List<LLVMValueRef>();
            var localVariableTypes = new List<TypeReference>();
            var branchTo = new Dictionary<Instruction, LLVMBasicBlockRef>();
            var branchToProcess = new Stack<KeyValuePair<Instruction, LLVMBasicBlockRef>>();
            var processedBranch = new List<Instruction>();
            LLVMTypeRef functionType;
            if (method.HasThis)
            {
                functionType = LLVM.FunctionType(
                    ResolveType(method.ReturnType).FieldTypeRef,
                    method.Parameters.Select(p => ResolveType(p.ParameterType).FieldTypeRef)
                        .Prepend(LLVM.PointerType(FullSymbolToTypeRef[method.DeclaringType.FullName].StructTypeRef, 0)).ToArray(),
                    false
                );
            }
            else
            {
                functionType = LLVM.FunctionType(
                    ResolveType(method.ReturnType).FieldTypeRef,
                    method.Parameters.Select(p => ResolveType(p.ParameterType).FieldTypeRef).ToArray(),
                    false
                );
            }

            var entryFunctionSymbol = GetCSToLLVMSymbolName(method);
            LLVMValueRef entryFunction;

            if (method.IsPInvokeImpl)
            {
                entryFunction = LLVM.AddFunction(llvmModuleRef, method.PInvokeInfo.EntryPoint ?? entryFunctionSymbol,
                    functionType);
                if (method.PInvokeInfo.Module.Name != "__internal")
                    throw new NotSupportedException("Does not support actual P/Invoke just yet.");

                SymbolToCallableFunction.Add(GetCSToLLVMSymbolName(method), entryFunction);
                return;
            }

            entryFunction = LLVM.AddFunction(llvmModuleRef, isMain ? "main" : entryFunctionSymbol, functionType);

            var entryBlock = LLVM.AppendBasicBlock(entryFunction, "entry");
            var builder = LLVM.CreateBuilder();
            LLVM.PositionBuilderAtEnd(builder, entryBlock);
            foreach (var local in method.Body.Variables)
            {
                Debug(local.VariableType.FullName);
                if (local.VariableType.IsPointer)
                {
                    localVariableTypes.Add(local.VariableType);
                    var alloca = LLVM.BuildAlloca(builder, ResolveLLVMType(local.VariableType), $"Alloca_{local.VariableType.Name}");
                    Debug($"Local variable defined: {local.VariableType.Name} - {alloca} with Type Of {alloca.TypeOf()}");
                    localVariables.Add(alloca);
                }
                else if (local.VariableType.IsPrimitive)
                {
                    var type = ResolveLLVMType(local.VariableType);
                    localVariableTypes.Add(local.VariableType);
                    var alloca = LLVM.BuildAlloca(builder, type, $"Alloca_{local.VariableType.Name}");
                    Debug($"Local variable defined: {local.VariableType.Name} - {alloca} with Type Of {alloca.TypeOf()}");
                    localVariables.Add(alloca);
                }
                else
                {
                    localVariableTypes.Add(local.VariableType);
                    if (!FullSymbolToTypeRef.ContainsKey(local.VariableType.FullName))
                        FullSymbolToTypeRef.Add(local.VariableType.FullName, ProcessForStruct(local.VariableType.Resolve()));
                    var alloca = LLVM.BuildAlloca(builder,
                        LLVM.PointerType(FullSymbolToTypeRef[local.VariableType.FullName].StructTypeRef, 0),
                        $"Alloca_{local.VariableType.MakePointerType().Name}");
                    Debug($"Local variable defined: {local.VariableType.Name} - {alloca} with Type Of {alloca.TypeOf()}");
                    localVariables.Add(alloca);
                }
            }
            
            // Handle Branch Blocks
            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.OpCode == OpCodes.Br_S ||
                    instruction.OpCode == OpCodes.Br ||
                    instruction.OpCode == OpCodes.Brtrue||
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
                    var operand = (Instruction)instruction.Operand;
                    var branch = LLVM.AppendBasicBlock(entryFunction, "branch");
                    branchTo.TryAdd(operand, branch);
                    branchToProcess.Push(new KeyValuePair<Instruction, LLVMBasicBlockRef>(operand, branch));
                    Debug($"Branch to {operand}");
                }
            }

            LLVMValueRef ProcessStore(LLVMValueRef lval, LLVMValueRef rval)
            {
                var lvalType = lval.TypeOf();
                var rvalType = rval.TypeOf();
                if (!lvalType.Equals(LLVM.GetElementType(rvalType)))
                {
                    lval = AutoCast(builder, lval, rvalType.GetElementType());
                }
                return LLVM.BuildStore(builder, lval, rval);
            }
            void ProcessStoreLoc(BuilderStackItem stackItem, int localVariableIndex)
            {
                var localVariableType = localVariableTypes[localVariableIndex];
                var localVariable = localVariables[localVariableIndex];
                //TODO: Need a better way to compare...
                if (localVariableType.IsPointer)
                {
                    var resolvePtrType = ResolveLLVMType(localVariableType);
                    var store = ProcessStore(LLVM.BuildIntToPtr(builder, stackItem.ValRef.Value, resolvePtrType, "IntPtr"), localVariable);
                    Debug($"[Stloc_{localVariableIndex}] -> Popped {stackItem.ValRef.Value.TypeOf()} and Stored {store}");
                }
                else
                {
                    var store = ProcessStore(stackItem.ValRef.Value, localVariable);
                    Debug(
                        $"[Stloc_{localVariableIndex}] -> Popped {stackItem.ValRef.Value.TypeOf()} and Stored {store}");
                }
            }

            void ProcessInstructions(Instruction paramInstruction)
            {
                var isFirst = true;
                var instruction = paramInstruction;
                do
                {
                    if (isFirst)
                        isFirst = false;
                    else
                    {
                        instruction = instruction.Next;
                    }

                    if (instruction.OpCode == OpCodes.Ldarg_0)
                    {
                        if (!method.HasThis)
                            builderStack.Push(new BuilderStackItem(method.Parameters[0].ParameterType,
                                LLVM.GetFirstParam(entryFunction)));
                        else
                            builderStack.Push(new BuilderStackItem(method.DeclaringType,
                                LLVM.GetFirstParam(entryFunction)));

                        Debug($"[Ldarg_0] -> {LLVM.GetFirstParam(entryFunction)} pushed to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldarg_1)
                    {
                        builderStack.Push(
                            new BuilderStackItem(method.Parameters[method.HasThis ? 0 : 1].ParameterType,
                                LLVM.GetParam(entryFunction, 1)));
                        Debug($"[Ldarg_1] -> {LLVM.GetParams(entryFunction)[1]} pushed to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldarg_2)
                    {
                        builderStack.Push(
                            new BuilderStackItem(method.Parameters[method.HasThis ? 0 : 1].ParameterType,
                                LLVM.GetParam(entryFunction, 2)));
                        Debug($"[Ldarg_2] -> {LLVM.GetParams(entryFunction)[2]} pushed to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldarg_3)
                    {
                        builderStack.Push(
                            new BuilderStackItem(method.Parameters[method.HasThis ? 0 : 1].ParameterType,
                                LLVM.GetParam(entryFunction, 3)));
                        Debug($"[Ldarg_3] -> {LLVM.GetParams(entryFunction)[3]} pushed to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldarg)
                    {
                        var varDef = (VariableDefinition) instruction.Operand;
                        builderStack.Push(new BuilderStackItem(
                            method.Parameters[method.HasThis ? varDef.Index - 1 : varDef.Index].ParameterType,
                            LLVM.GetParam(entryFunction, (uint) varDef.Index)));
                        Debug($"[Ldarg {varDef.Index}] -> {LLVM.GetParams(entryFunction)[1]} pushed to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Nop)
                    {
                        Debug("[Nop]");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldloca_S)
                    {
                        var def = (VariableDefinition) instruction.Operand;
                        var stackItem = new BuilderStackItem(method.Body.Variables[def.Index].VariableType,
                            localVariables[def.Index]);
                        builderStack.Push(stackItem);
                        Debug(
                            $"[Ldloca_S {def.Index}] -> Pushed Local Variable {stackItem.ValRef.Value} to Stack");
                        continue;
                    }
                    
                    if (instruction.OpCode == OpCodes.Ldloc_S)
                    {
                        var def = (VariableDefinition) instruction.Operand;
                        var stackItem = new BuilderStackItem(method.Body.Variables[def.Index].VariableType,
                            LLVM.BuildLoad(builder, localVariables[def.Index], ""));
                        builderStack.Push(stackItem);
                        Debug(
                            $"[Ldloc_S {def.Index}] -> Pushed Local Variable {stackItem.ValRef.Value} to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldloc_0)
                    {
                        var stackItem = new BuilderStackItem(method.Body.Variables[0].VariableType,
                            LLVM.BuildLoad(builder, localVariables[0], ""));
                        builderStack.Push(stackItem);
                        Debug($"[Ldloc_0] -> Pushed Local Variable {stackItem.ValRef.Value} to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldloc_1)
                    {
                        var stackItem = new BuilderStackItem(method.Body.Variables[1].VariableType,
                            LLVM.BuildLoad(builder, localVariables[1], ""));
                        builderStack.Push(stackItem);
                        Debug($"[Ldloc_1] -> Pushed Local Variable {stackItem.ValRef.Value} to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldloc_2)
                    {
                        var stackItem = new BuilderStackItem(method.Body.Variables[2].VariableType,
                            LLVM.BuildLoad(builder, localVariables[2], ""));
                        builderStack.Push(stackItem);
                        Debug($"[Ldloc_2] -> Pushed Local Variable {stackItem.ValRef.Value} to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldloc_3)
                    {
                        var stackItem = new BuilderStackItem(method.Body.Variables[3].VariableType,
                            LLVM.BuildLoad(builder, localVariables[3], ""));
                        builderStack.Push(stackItem);
                        Debug($"[Ldloc_3] -> Pushed Local Variable {stackItem.ValRef.Value} to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldind_U1)
                    {
                        var val = builderStack.Pop();
                        LLVMValueRef cast;
                        if (val.Type.IsPointer)
                            cast = LLVM.BuildPtrToInt(builder, LLVM.BuildLoad(builder, val.ValRef.Value, ""),
                                LLVM.Int32Type(), "");
                        else
                            cast = LLVM.BuildZExt(builder, LLVM.BuildLoad(builder, val.ValRef.Value, ""),
                                LLVM.Int32Type(), "");
                        builderStack.Push(new BuilderStackItem
                        {
                            Type = MiniBCL.Int32Type,
                            TypeRef = LLVM.Int32Type(),
                            ValRef = cast
                        });
                        Debug(
                            $"[Ldind_U1] -> Popped Stack Item {val.ValRef.Value}, Loaded and Casted to Int32 Type {cast}");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldind_I)
                    {
                        var val = builderStack.Pop();
                        // TODO: Support Native Integer conversion
                        LLVMValueRef cast;
                        if (val.Type.IsPointer)
                        {
                            cast = LLVM.BuildLoad(builder, val.ValRef.Value, "");
                            builderStack.Push(new BuilderStackItem
                            {
                                Type = ((PointerType) val.Type).ElementType,
                                TypeRef = cast.TypeOf(),
                                ValRef = cast
                            });
                        }
                        else
                        {
                            cast = LLVM.BuildZExt(builder,
                                LLVM.BuildLoad(builder, val.ValRef.Value, $"Load_{val.Type.Name}"),
                                LLVM.Int64Type(), "");
                            builderStack.Push(new BuilderStackItem
                            {
                                Type = MiniBCL.Int64Type,
                                TypeRef = LLVM.Int64Type(),
                                ValRef = cast
                            });
                        }

                        Debug(
                            $"[Ldind_I] -> Popped Stack Item {val.ValRef.Value} and Casted to Int64 Type {cast}");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Initobj)
                    {
                        var item = builderStack.Pop();
                        Debug($"[Initobj] -> Popped Stack Item {item.ValRef.Value}");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldc_I4)
                    {
                        var operand = (int) instruction.Operand;
                        var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(llvmContextRef), (ulong) operand, true);
                        builderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                            stackItem));
                        Debug($"[Ldc_I4 {operand}] -> Pushed {stackItem} to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldc_I4_S)
                    {
                        var operand = (sbyte) instruction.Operand;
                        var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(llvmContextRef), (ulong) operand, true);
                        builderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                            stackItem));
                        Debug($"[Ldc_I4_S {operand}] -> Pushed {stackItem} to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldc_I4_0)
                    {
                        var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(llvmContextRef), 0, true);
                        builderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                            stackItem));
                        Debug($"[Ldc_I4_0] -> Pushed {stackItem} to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldc_I4_1)
                    {
                        var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(llvmContextRef), 1, true);
                        builderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                            stackItem));
                        Debug($"[Ldc_I4_1] -> Pushed {stackItem} to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldc_I4_2)
                    {
                        var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(llvmContextRef), 2, true);
                        builderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                            stackItem));
                        Debug($"[Ldc_I4_2] -> Pushed {stackItem} to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldc_I4_3)
                    {
                        var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(llvmContextRef), 3, true);
                        builderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                            stackItem));
                        Debug($"[Ldc_I4_3] -> Pushed {stackItem} to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldc_I4_4)
                    {
                        var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(llvmContextRef), 4, true);
                        builderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                            stackItem));
                        Debug($"[Ldc_I4_4] -> Pushed {stackItem} to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldc_I4_5)
                    {
                        var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(llvmContextRef), 5, true);
                        builderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                            stackItem));
                        Debug($"[Ldc_I4_5] -> Pushed {stackItem} to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldc_I4_6)
                    {
                        var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(llvmContextRef), 6, true);
                        builderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                            stackItem));
                        Debug($"[Ldc_I4_6] -> Pushed {stackItem} to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldc_I4_7)
                    {
                        var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(llvmContextRef), 7, true);
                        builderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                            stackItem));
                        Debug($"[Ldc_I4_7] -> Pushed {stackItem} to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldc_I4_8)
                    {
                        var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(llvmContextRef), 8, true);
                        builderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                            stackItem));
                        Debug($"[Ldc_I4_8] -> Pushed {stackItem} to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Dup)
                    {
                        Debug("Attempting Dup");
                        var stackItem = builderStack.Peek();
                        builderStack.Push(stackItem);
                        Debug($"[Dup] -> Pushed a Duplicate of {stackItem.ValRef.Value} onto Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldc_I4_M1)
                    {
                        unchecked
                        {
                            var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(llvmContextRef), (ulong) -1, true);
                            builderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                                stackItem));
                            Debug($"[Ldc_I4_M1] -> Pushed {stackItem} to Stack");
                        }

                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldc_I8)
                    {
                        var stackItem = new BuilderStackItem(MiniBCL.Int64Type,
                            LLVM.ConstInt(LLVM.Int64TypeInContext(llvmContextRef), (ulong) instruction.Operand,
                                new LLVMBool(1)));
                        builderStack.Push(stackItem);
                        Debug($"[Ldc_I8] -> Pushed {stackItem.ValRef.Value} to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldc_R4)
                    {
                        var stackItem = new BuilderStackItem(MiniBCL.FloatType,
                            LLVM.ConstReal(LLVM.FloatTypeInContext(llvmContextRef), (double) instruction.Operand));
                        builderStack.Push(stackItem);
                        Debug($"[Ldc_R4] -> Pushed {stackItem.ValRef.Value} to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldc_R8)
                    {
                        var stackItem = new BuilderStackItem(MiniBCL.DoubleType,
                            LLVM.ConstReal(LLVM.DoubleTypeInContext(llvmContextRef), (double) instruction.Operand));
                        builderStack.Push(stackItem);
                        Debug($"[Ldc_R8] -> Pushed {stackItem.ValRef.Value} to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldnull)
                    {
                        var stackItem = new BuilderStackItem(MiniBCL.Int64Type,
                            LLVM.ConstNull(LLVM.PointerType(LLVM.Int8Type(), 0)));
                        builderStack.Push(stackItem);
                        Debug($"[Ldnull] -> Pushed {stackItem.ValRef.Value} to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldftn)
                    {
                        var operand = (MethodDefinition) instruction.Operand;
                        var symbol = GetCSToLLVMSymbolName(operand);
                        if (!SymbolToCallableFunction.ContainsKey(symbol))
                        {
                            ProcessFunction(operand.Resolve(), false, indent + 1);
                        }

                        var stackItem =
                            new BuilderStackItem(operand.ReturnType.Resolve(), SymbolToCallableFunction[symbol]);
                        builderStack.Push(stackItem);
                        Debug($"[Ldftn {stackItem.Type}] -> Pushed {stackItem.ValRef.Value} to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Newobj)
                    {
                        var operand = (MethodDefinition) instruction.Operand;
                        LLVMValueRef valRef;
                        if (operand.DeclaringType.IsClass)
                            valRef = LLVM.BuildMalloc(builder,
                                FullSymbolToTypeRef[operand.DeclaringType.FullName].StructTypeRef,
                                $"Malloc_{operand.DeclaringType.Name}");
                        else
                            valRef = LLVM.BuildAlloca(builder,
                                FullSymbolToTypeRef[operand.DeclaringType.FullName].StructTypeRef,
                                $"Alloc_{operand.DeclaringType.Name}");
                        builderStack.Push(new BuilderStackItem(operand.DeclaringType, valRef));
                        ProcessCall(instruction, builder, indent, builderStack);
                        builderStack.Push(new BuilderStackItem(operand.DeclaringType, valRef));
                        Debug(
                            $"[Newobj {operand.FullName}] -> Called Ctor and pushed {builderStack.Peek().ValRef.Value} to stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Stind_I1)
                    {
                        var val = builderStack.Pop();
                        var cast = LLVM.BuildZExt(builder, val.ValRef.Value, LLVM.Int8Type(), "");
                        var address = builderStack.Pop();
                        var ptr = address.ValRef.Value;
                        if (address.ValRef.Value.TypeOf().TypeKind != LLVMTypeKind.LLVMPointerTypeKind)
                            ptr = LLVM.BuildIntToPtr(builder, address.ValRef.Value,
                                LLVM.PointerType(LLVM.Int8Type(), 0),
                                "");
                        var store = ProcessStore(cast, ptr);
                        clrLogger.Debug(
                            $"[Stind_I1] -> Popped {val.ValRef.Value} and {address.ValRef.Value} and Stored into address: {store}");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Conv_I)
                    {
                        var value = builderStack.Pop();
                        if (IsTypeARealNumber(value.Type))
                        {
                            var stackItem = new BuilderStackItem(MiniBCL.Int32Type,
                                LLVM.BuildFPToUI(builder, value.ValRef.Value, LLVM.Int8Type(), ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Conv_U1] -> Popped {value.ValRef.Value} and Pushed As Unsigned Integer {stackItem.ValRef.Value} to Stack");
                        }
                        else if (IsTypeAnInteger(value.Type))
                        {
                            var stackItem = new BuilderStackItem(MiniBCL.Int32Type,
                                LLVM.BuildZExt(builder, value.ValRef.Value, LLVM.Int8Type(), ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Conv_U1] -> Popped {value.ValRef.Value} and Pushed As Unsigned Integer {stackItem.ValRef.Value} to Stack");
                        }
                        // TODO: Support Decimal Conversion To Int64
                        else
                            throw new Exception(
                                "INTEGER 8 BYTES CONVERSION IS NOT SUPPORTED");

                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Conv_U1)
                    {
                        var value = builderStack.Pop();
                        if (IsTypeARealNumber(value.Type))
                        {
                            var stackItem = new BuilderStackItem(MiniBCL.UInt8Type,
                                LLVM.BuildFPToUI(builder, value.ValRef.Value, LLVM.Int8Type(), ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Conv_U1] -> Popped {value.ValRef.Value} and Pushed As Unsigned Integer {stackItem.ValRef.Value} to Stack");
                        }
                        else if (IsTypeAnInteger(value.Type))
                        {
                            var stackItem = new BuilderStackItem(MiniBCL.UInt8Type,
                                LLVM.BuildZExt(builder, value.ValRef.Value, LLVM.Int8Type(), ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Conv_U1] -> Popped {value.ValRef.Value} and Pushed As Unsigned Integer {stackItem.ValRef.Value} to Stack");
                        }
                        // TODO: Support Decimal Conversion To Int64
                        else
                            throw new Exception(
                                "UNSIGNED INTEGER 1 BYTES CONVERSION IS NOT SUPPORTED");

                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Conv_U2)
                    {
                        var value = builderStack.Pop();
                        if (IsTypeARealNumber(value.Type))
                        {
                            var stackItem = new BuilderStackItem(MiniBCL.UInt16Type,
                                LLVM.BuildFPToUI(builder, value.ValRef.Value, LLVM.Int16Type(), ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Conv_U2] -> Popped {value.ValRef.Value} and Pushed As Unsigned Integer {stackItem.ValRef.Value} to Stack");
                        }
                        else if (IsTypeAnInteger(value.Type))
                        {
                            var stackItem = new BuilderStackItem(MiniBCL.UInt16Type,
                                LLVM.BuildZExt(builder, value.ValRef.Value, LLVM.Int16Type(), ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Conv_U2] -> Popped {value.ValRef.Value} and Pushed As Unsigned Integer {stackItem.ValRef.Value} to Stack");
                        }
                        // TODO: Support Decimal Conversion To Int64
                        else
                            throw new Exception(
                                "UNSIGNED INTEGER 2 BYTES CONVERSION IS NOT SUPPORTED");

                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Conv_U4)
                    {
                        var value = builderStack.Pop();
                        if (IsTypeARealNumber(value.Type))
                        {
                            var stackItem = new BuilderStackItem(MiniBCL.UInt32Type,
                                LLVM.BuildFPToUI(builder, value.ValRef.Value, LLVM.Int32Type(), ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Conv_U4] -> Popped {value.ValRef.Value} and Pushed As Unsigned Integer {stackItem.ValRef.Value} to Stack");
                        }
                        else if (IsTypeAnInteger(value.Type))
                        {
                            var stackItem = new BuilderStackItem(MiniBCL.UInt32Type,
                                LLVM.BuildZExt(builder, value.ValRef.Value, LLVM.Int32Type(), ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Conv_U4] -> Popped {value.ValRef.Value} and Pushed As Unsigned Integer {stackItem.ValRef.Value} to Stack");
                        }
                        // TODO: Support Decimal Conversion To Int64
                        else
                            throw new Exception(
                                "UNSIGNED INTEGER 4 BYTES CONVERSION IS NOT SUPPORTED");

                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Conv_U8)
                    {
                        var value = builderStack.Pop();
                        if (IsTypeARealNumber(value.Type))
                        {
                            var stackItem = new BuilderStackItem(MiniBCL.UInt64Type,
                                LLVM.BuildFPToUI(builder, value.ValRef.Value, LLVM.Int64Type(), ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Conv_U8] -> Popped {value.ValRef.Value} and Pushed As Unsigned Integer {stackItem.ValRef.Value} to Stack");
                        }
                        else if (IsTypeAnInteger(value.Type))
                        {
                            var stackItem = new BuilderStackItem(MiniBCL.UInt64Type,
                                LLVM.BuildZExt(builder, value.ValRef.Value, LLVM.Int64Type(), ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Conv_U8] -> Popped {value.ValRef.Value} and Pushed As Unsigned Integer {stackItem.ValRef.Value} to Stack");
                        }
                        // TODO: Support Decimal Conversion To Int64
                        else
                            throw new Exception(
                                "UNSIGNED INTEGER 8 BYTES CONVERSION IS NOT SUPPORTED");

                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Conv_I1)
                    {
                        var value = builderStack.Pop();
                        if (IsTypeARealNumber(value.Type))
                        {
                            var stackItem = new BuilderStackItem(MiniBCL.Int8Type,
                                LLVM.BuildFPToSI(builder, value.ValRef.Value, LLVM.Int8Type(), ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Conv_I1] -> Popped {value.ValRef.Value} and Pushed As Signed Integer {stackItem.ValRef.Value} to Stack");
                        }
                        else if (IsTypeAnInteger(value.Type))
                        {
                            var stackItem = new BuilderStackItem(MiniBCL.Int8Type,
                                LLVM.BuildZExt(builder, value.ValRef.Value, LLVM.Int8Type(), ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Conv_I1] -> Popped {value.ValRef.Value} and Pushed As Signed Integer {stackItem.ValRef.Value} to Stack");
                        }
                        // TODO: Support Decimal Conversion To Int64
                        else
                            throw new Exception(
                                "INTEGER 1 BYTES CONVERSION IS NOT SUPPORTED");

                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Conv_I2)
                    {
                        var value = builderStack.Pop();
                        if (IsTypeARealNumber(value.Type))
                        {
                            var stackItem = new BuilderStackItem(MiniBCL.Int16Type,
                                LLVM.BuildFPToSI(builder, value.ValRef.Value, LLVM.Int16Type(), ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Conv_I2] -> Popped {value.ValRef.Value} and Pushed As Signed Integer {stackItem.ValRef.Value} to Stack");
                        }
                        else if (IsTypeAnInteger(value.Type))
                        {
                            var stackItem = new BuilderStackItem(MiniBCL.Int16Type,
                                LLVM.BuildZExt(builder, value.ValRef.Value, LLVM.Int16Type(), ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Conv_I2] -> Popped {value.ValRef.Value} and Pushed As Signed Integer {stackItem.ValRef.Value} to Stack");
                        }
                        // TODO: Support Decimal Conversion To Int64
                        else
                            throw new Exception(
                                "INTEGER 2 BYTES CONVERSION IS NOT SUPPORTED");

                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Conv_I4)
                    {
                        var value = builderStack.Pop();
                        if (value.Type.IsPointer)
                        {
                            var stackItem = new BuilderStackItem(MiniBCL.Int32Type,
                                LLVM.BuildPtrToInt(builder, value.ValRef.Value, LLVM.Int32Type(), ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Conv_I4] -> Popped {value.ValRef.Value} and Pushed As Signed Integer {stackItem.ValRef.Value} to Stack");
                        }
                        else if (IsTypeARealNumber(value.Type))
                        {
                            var stackItem = new BuilderStackItem(MiniBCL.Int32Type,
                                LLVM.BuildFPToSI(builder, value.ValRef.Value, LLVM.Int32Type(), ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Conv_I4] -> Popped {value.ValRef.Value} and Pushed As Signed Integer {stackItem.ValRef.Value} to Stack");
                        }
                        else if (IsTypeAnInteger(value.Type))
                        {
                            var stackItem = new BuilderStackItem(MiniBCL.Int32Type,
                                LLVM.BuildZExt(builder, value.ValRef.Value, LLVM.Int32Type(), ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Conv_I4] -> Popped {value.ValRef.Value} and Pushed As Signed Integer {stackItem.ValRef.Value} to Stack");
                        }
                        // TODO: Support Decimal Conversion To Int64
                        else
                            throw new Exception(
                                "INTEGER 4 BYTES CONVERSION IS NOT SUPPORTED");

                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Conv_I8)
                    {
                        var value = builderStack.Pop();
                        if (IsTypeARealNumber(value.Type))
                        {
                            var stackItem = new BuilderStackItem(MiniBCL.Int64Type,
                                LLVM.BuildFPToSI(builder, value.ValRef.Value, LLVM.Int64Type(), ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Conv_I8] -> Popped {value.ValRef.Value} and Pushed As Signed Integer {stackItem.ValRef.Value} to Stack");
                        }
                        else if (IsTypeAnInteger(value.Type))
                        {
                            var stackItem = new BuilderStackItem(MiniBCL.Int64Type,
                                LLVM.BuildZExt(builder, value.ValRef.Value, LLVM.Int64Type(), ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Conv_I8] -> Popped {value.ValRef.Value} and Pushed As Signed Integer {stackItem.ValRef.Value} to Stack");
                        }
                        // TODO: Support Decimal Conversion To Int64
                        else
                            throw new Exception(
                                "INTEGER 8 BYTES CONVERSION IS NOT SUPPORTED");

                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Conv_R4)
                    {
                        var value = builderStack.Pop();
                        if (IsTypeAnInteger(value.Type))
                        {
                            var stackItem = new BuilderStackItem(MiniBCL.FloatType,
                                LLVM.BuildSIToFP(builder, value.ValRef.Value, LLVM.FloatTypeInContext(llvmContextRef),
                                    ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Conv_R4] -> Popped {value.ValRef.Value} and Pushed As Float {stackItem.ValRef.Value} to Stack");
                        }
                        else if (IsTypeARealNumber(value.Type))
                        {
                            var stackItem = new BuilderStackItem(MiniBCL.FloatType,
                                LLVM.BuildFPCast(builder, value.ValRef.Value, LLVM.FloatTypeInContext(llvmContextRef),
                                    ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Conv_R4] -> Popped {value.ValRef.Value} and Pushed As Float {stackItem.ValRef.Value} to Stack");
                        }
                        // TODO: Support Decimal Conversion to Real 32 bit
                        else
                        {
                            throw new Exception(
                                "REAL 4 BYTES CONVERSION IS NOT SUPPORTED");
                        }

                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Conv_R8)
                    {
                        var value = builderStack.Pop();
                        if (IsTypeAnInteger(value.Type))
                        {
                            var stackItem = new BuilderStackItem(MiniBCL.DoubleType,
                                LLVM.BuildSIToFP(builder, value.ValRef.Value, LLVM.DoubleTypeInContext(llvmContextRef),
                                    ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Conv_R8] -> Popped {value.ValRef.Value} and Pushed As Float {stackItem.ValRef.Value} to Stack");
                        }
                        else if (IsTypeARealNumber(value.Type))
                        {
                            var stackItem = new BuilderStackItem(MiniBCL.DoubleType,
                                LLVM.BuildFPCast(builder, value.ValRef.Value, LLVM.DoubleTypeInContext(llvmContextRef),
                                    ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Conv_R8] -> Popped {value.ValRef.Value} and Pushed As Float {stackItem.ValRef.Value} to Stack");
                        }
                        // TODO: Support Decimal Conversion to Real 64 bit
                        else
                        {
                            throw new Exception(
                                "REAL 8 BYTES CONVERSION IS NOT SUPPORTED");
                        }

                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Stfld)
                    {
                        var fieldDef = (FieldDefinition) instruction.Operand;
                        var value = builderStack.Pop();
                        var structRef = builderStack.Pop();
                        if (value.ValRef.Value.IsNull())
                            throw new Exception(
                                "The Value/Reference returned as null thus cannot be stored in Struct!");
                        if (structRef.ValRef.Value.IsNull())
                            throw new Exception(
                                "The Value/Reference returned as null thus cannot be stored in Struct!");

                        var index = (uint)  Array.IndexOf(FullSymbolToTypeRef[fieldDef.DeclaringType.FullName].CS_FieldDefs, 
                            FullSymbolToTypeRef[fieldDef.DeclaringType.FullName].CS_FieldDefs
                            .First(I => I.Name == fieldDef.Name));

                        var refToStruct = structRef.ValRef.Value;
                        LLVMValueRef offset;
                        if (fieldDef.DeclaringType.IsClass)
                        {
                            offset = LLVM.BuildLoad(builder, refToStruct, "");
                        }

                        offset = LLVM.BuildStructGEP(builder, refToStruct, index, structRef.Type.Name);
                        ProcessStore(value.ValRef.Value, offset);
                        Debug(
                            $"[Stfld {fieldDef.FullName}] -> Popped {value.ValRef.Value} and {refToStruct.TypeOf()} and Store {value.ValRef.Value} into {offset}");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Stloc_0)
                    {
                        var val = builderStack.Pop();
                        ProcessStoreLoc(val, 0);
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Stloc_1)
                    {
                        var val = builderStack.Pop();
                        ProcessStoreLoc(val, 1);
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Stloc_2)
                    {
                        var val = builderStack.Pop();
                        ProcessStoreLoc(val, 2);
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Stloc_3)
                    {
                        var val = builderStack.Pop();
                        ProcessStoreLoc(val, 3);
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Stloc_S)
                    {
                        var index = (VariableDefinition) instruction.Operand;
                        var val = builderStack.Pop();
                        ProcessStoreLoc(val, index.Index);
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldfld)
                    {
                        var fieldDef = (FieldDefinition) instruction.Operand;
                        var structRef = builderStack.Pop();
                        if (!structRef.ValRef.HasValue)
                            throw new Exception(
                                "The Value/Reference returned as null thus cannot be stored in Struct!");
                        //var load = LLVM.BuildLoad(builder, structRef.ValRef.Value, $"Loaded_{structRef.Type.Name}");
                        var load = structRef.ValRef.Value;
                        var offset = LLVM.BuildStructGEP(builder, load,
                            (uint) Array.IndexOf(FullSymbolToTypeRef[fieldDef.DeclaringType.FullName].CS_FieldDefs,
                                FullSymbolToTypeRef[fieldDef.DeclaringType.FullName].CS_FieldDefs
                                .First(I => I.Name == fieldDef.Name)), $"Offset_{fieldDef.Name}");
                        var item = new BuilderStackItem(fieldDef.FieldType, LLVM.BuildLoad(builder, offset, ""));
                        builderStack.Push(item);
                        Debug(
                            $"[Ldfld {fieldDef.FullName}] -> Popped {structRef.ValRef.Value} off stack and pushed {item.ValRef.Value} to stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ldstr)
                    {
                        var val = (string) instruction.Operand;
                        var ldstr = LLVM.BuildGlobalStringPtr(builder, val, "");
                        var stackItem = new BuilderStackItem(MiniBCL.StringType, ldstr);
                        builderStack.Push(stackItem);
                        Debug($"[Ldstr {val}] -> Pushed {ldstr.TypeOf()} to Stack");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Callvirt ||
                        instruction.OpCode == OpCodes.Call)
                    {
                        ProcessCall(instruction, builder, indent, builderStack);
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Add)
                    {
                        // TODO: Support conversion between Floating Point and Integers
                        var rval = builderStack.Pop();
                        var lval = builderStack.Pop();
                        if (IsTypeAnInteger(lval.Type) && IsTypeAnInteger(rval.Type))
                        {
                            // TODO: Need to determine the size of pointer.
                            LLVMValueRef actualLVal;
                            LLVMValueRef actualRVal;
                            if (lval.ValRef.Value.TypeOf().TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
                            {
                                actualLVal = LLVM.BuildPtrToInt(builder, lval.ValRef.Value, LLVM.Int32Type(), "lval");
                            }
                            else
                                actualLVal = LLVM.BuildZExt(builder, lval.ValRef.Value, LLVM.Int32Type(), "lval");

                            if (rval.ValRef.Value.TypeOf().TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
                            {
                                actualRVal = LLVM.BuildPtrToInt(builder, rval.ValRef.Value, LLVM.Int32Type(), "rval");
                            }
                            else
                                actualRVal = LLVM.BuildZExt(builder, rval.ValRef.Value, LLVM.Int32Type(), "rval");

                            var stackItem = new BuilderStackItem(lval.Type,
                                LLVM.BuildAdd(builder, actualLVal, actualRVal, ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Add] -> Popped {rval.ValRef.Value} and {lval.ValRef.Value.TypeOf()} and Pushed {stackItem.ValRef.Value}");
                        }
                        else if (IsTypeARealNumber(lval.Type) && IsTypeARealNumber(rval.Type))
                        {
                            var stackItem = new BuilderStackItem(lval.Type,
                                LLVM.BuildFAdd(builder, lval.ValRef.Value, rval.ValRef.Value, ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Add] -> Popped {rval.ValRef.Value} and {lval.ValRef.Value} and Pushed {stackItem.ValRef.Value}");
                        }
                        else
                        {
                            throw new Exception("Unknown type, thus cannot add!");
                        }

                        continue;
                    }
                    
                    if (instruction.OpCode == OpCodes.Sub)
                    {
                        // TODO: Support conversion between Floating Point and Integers
                        var rval = builderStack.Pop();
                        var lval = builderStack.Pop();
                        if (IsTypeAnInteger(lval.Type) && IsTypeAnInteger(rval.Type))
                        {
                            // TODO: Need to determine the size of pointer.
                            LLVMValueRef actualLVal;
                            LLVMValueRef actualRVal;
                            if (lval.ValRef.Value.TypeOf().TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
                            {
                                actualLVal = LLVM.BuildPtrToInt(builder, lval.ValRef.Value, LLVM.Int32Type(), "lval");
                            }
                            else
                                actualLVal = LLVM.BuildZExt(builder, lval.ValRef.Value, LLVM.Int32Type(), "lval");

                            if (rval.ValRef.Value.TypeOf().TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
                            {
                                actualRVal = LLVM.BuildPtrToInt(builder, rval.ValRef.Value, LLVM.Int32Type(), "rval");
                            }
                            else
                                actualRVal = LLVM.BuildZExt(builder, rval.ValRef.Value, LLVM.Int32Type(), "rval");

                            var stackItem = new BuilderStackItem(lval.Type,
                                LLVM.BuildSub(builder, actualLVal, actualRVal, ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Sub] -> Popped {rval.ValRef.Value} and {lval.ValRef.Value} and Pushed {stackItem.ValRef.Value}");
                        }
                        else if (IsTypeARealNumber(lval.Type) && IsTypeARealNumber(rval.Type))
                        {
                            var stackItem = new BuilderStackItem(lval.Type,
                                LLVM.BuildFSub(builder, lval.ValRef.Value, rval.ValRef.Value, ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Sub] -> Popped {rval.ValRef.Value} and {lval.ValRef.Value} and Pushed {stackItem.ValRef.Value}");
                        }
                        else
                        {
                            throw new Exception("Unknown type, thus cannot add!");
                        }

                        continue;
                    }
                    
                    if (instruction.OpCode == OpCodes.Mul)
                    {
                        // TODO: Support conversion between Floating Point and Integers
                        var rval = builderStack.Pop();
                        var lval = builderStack.Pop();
                        if (IsTypeAnInteger(lval.Type) && IsTypeAnInteger(rval.Type))
                        {
                            // TODO: Need to determine the size of pointer.
                            LLVMValueRef actualLVal;
                            LLVMValueRef actualRVal;
                            if (lval.ValRef.Value.TypeOf().TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
                            {
                                actualLVal = LLVM.BuildPtrToInt(builder, lval.ValRef.Value, LLVM.Int32Type(), "lval");
                            }
                            else
                                actualLVal = LLVM.BuildZExt(builder, lval.ValRef.Value, LLVM.Int32Type(), "lval");

                            if (rval.ValRef.Value.TypeOf().TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
                            {
                                actualRVal = LLVM.BuildPtrToInt(builder, rval.ValRef.Value, LLVM.Int32Type(), "rval");
                            }
                            else
                                actualRVal = LLVM.BuildZExt(builder, rval.ValRef.Value, LLVM.Int32Type(), "rval");

                            var stackItem = new BuilderStackItem(lval.Type,
                                LLVM.BuildMul(builder, actualLVal, actualRVal, ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Sub] -> Popped {rval.ValRef.Value} and {lval.ValRef.Value} and Pushed {stackItem.ValRef.Value}");
                        }
                        else if (IsTypeARealNumber(lval.Type) && IsTypeARealNumber(rval.Type))
                        {
                            var stackItem = new BuilderStackItem(lval.Type,
                                LLVM.BuildFMul(builder, lval.ValRef.Value, rval.ValRef.Value, ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Sub] -> Popped {rval.ValRef.Value} and {lval.ValRef.Value} and Pushed {stackItem.ValRef.Value}");
                        }
                        else
                        {
                            throw new Exception("Unknown type, thus cannot add!");
                        }

                        continue;
                    }
                    
                    if (instruction.OpCode == OpCodes.Div)
                    {
                        // TODO: Support conversion between Floating Point and Integers
                        var rval = builderStack.Pop();
                        var lval = builderStack.Pop();
                        if (IsTypeAnInteger(lval.Type) && IsTypeAnInteger(rval.Type))
                        {
                            // TODO: Need to determine the size of pointer.
                            LLVMValueRef actualLVal;
                            LLVMValueRef actualRVal;
                            if (lval.ValRef.Value.TypeOf().TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
                            {
                                actualLVal = LLVM.BuildPtrToInt(builder, lval.ValRef.Value, LLVM.Int32Type(), "lval");
                            }
                            else
                                actualLVal = LLVM.BuildZExt(builder, lval.ValRef.Value, LLVM.Int32Type(), "lval");

                            if (rval.ValRef.Value.TypeOf().TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
                            {
                                actualRVal = LLVM.BuildPtrToInt(builder, rval.ValRef.Value, LLVM.Int32Type(), "rval");
                            }
                            else
                                actualRVal = LLVM.BuildZExt(builder, rval.ValRef.Value, LLVM.Int32Type(), "rval");

                            var stackItem = new BuilderStackItem(lval.Type,
                                LLVM.BuildSDiv(builder, actualLVal, actualRVal, ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Sub] -> Popped {rval.ValRef.Value} and {lval.ValRef.Value} and Pushed {stackItem.ValRef.Value}");
                        }
                        else if (IsTypeARealNumber(lval.Type) && IsTypeARealNumber(rval.Type))
                        {
                            var stackItem = new BuilderStackItem(lval.Type,
                                LLVM.BuildFDiv(builder, lval.ValRef.Value, rval.ValRef.Value, ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Sub] -> Popped {rval.ValRef.Value} and {lval.ValRef.Value} and Pushed {stackItem.ValRef.Value}");
                        }
                        else
                        {
                            throw new Exception("Unknown type, thus cannot add!");
                        }

                        continue;
                    }
                    
                    if (instruction.OpCode == OpCodes.Rem)
                    {
                        // TODO: Support conversion between Floating Point and Integers
                        var rval = builderStack.Pop();
                        var lval = builderStack.Pop();
                        if (IsTypeAnInteger(lval.Type) && IsTypeAnInteger(rval.Type))
                        {
                            // TODO: Need to determine the size of pointer.
                            LLVMValueRef actualLVal;
                            LLVMValueRef actualRVal;
                            if (lval.ValRef.Value.TypeOf().TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
                            {
                                actualLVal = LLVM.BuildPtrToInt(builder, lval.ValRef.Value, LLVM.Int32Type(), "lval");
                            }
                            else
                                actualLVal = LLVM.BuildZExt(builder, lval.ValRef.Value, LLVM.Int32Type(), "lval");

                            if (rval.ValRef.Value.TypeOf().TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
                            {
                                actualRVal = LLVM.BuildPtrToInt(builder, rval.ValRef.Value, LLVM.Int32Type(), "rval");
                            }
                            else
                                actualRVal = LLVM.BuildZExt(builder, rval.ValRef.Value, LLVM.Int32Type(), "rval");

                            var stackItem = new BuilderStackItem(lval.Type,
                                LLVM.BuildSRem(builder, actualLVal, actualRVal, ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Sub] -> Popped {rval.ValRef.Value} and {lval.ValRef.Value} and Pushed {stackItem.ValRef.Value}");
                        }
                        else if (IsTypeARealNumber(lval.Type) && IsTypeARealNumber(rval.Type))
                        {
                            var stackItem = new BuilderStackItem(lval.Type,
                                LLVM.BuildFRem(builder, lval.ValRef.Value, rval.ValRef.Value, ""));
                            builderStack.Push(stackItem);
                            Debug(
                                $"[Sub] -> Popped {rval.ValRef.Value} and {lval.ValRef.Value} and Pushed {stackItem.ValRef.Value}");
                        }
                        else
                        {
                            throw new Exception("Unknown type, thus cannot add!");
                        }

                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Box)
                    {
                        var operand = (TypeReference)instruction.Operand;
                        Debug("[Box] -> Allocated as Reference {0}");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Clt)
                    {
                        var rval = builderStack.Pop().ValRef.Value;
                        var lval = builderStack.Pop().ValRef.Value;
                        var rvalType = rval.TypeOf();
                        var lvalType = lval.TypeOf();
                        if (!rvalType.Equals(lvalType))
                        {
                            rval = AutoCast(builder, rval, lvalType);
                        }

                        LLVMValueRef cmp;
                        if (lvalType.TypeKind == LLVMTypeKind.LLVMIntegerTypeKind)
                            cmp = LLVM.BuildICmp(builder, LLVMIntPredicate.LLVMIntSLT, lval, rval, "clt");
                        else if (lvalType.TypeKind == LLVMTypeKind.LLVMFloatTypeKind)
                            cmp = LLVM.BuildFCmp(builder, LLVMRealPredicate.LLVMRealOLT, lval, rval, "clt");
                        else if (lvalType.TypeKind == LLVMTypeKind.LLVMDoubleTypeKind)
                            cmp = LLVM.BuildFCmp(builder, LLVMRealPredicate.LLVMRealOLT, lval, rval, "clt");
                        else
                        {
                            throw new NotImplementedException(
                                $"No comparision supported for those types: {lval} < {rval}");
                        }
                        builderStack.Push(new BuilderStackItem(MiniBCL.Int32Type, cmp));
                        continue;
                    }
                    
                    if (instruction.OpCode == OpCodes.Cgt)
                    {
                        var rval = builderStack.Pop().ValRef.Value;
                        var lval = builderStack.Pop().ValRef.Value;
                        var rvalType = rval.TypeOf();
                        var lvalType = lval.TypeOf();
                        if (!rvalType.Equals(lvalType))
                        {
                            rval = AutoCast(builder, rval, lvalType);
                        }

                        LLVMValueRef cmp;
                        if (lvalType.TypeKind == LLVMTypeKind.LLVMIntegerTypeKind)
                            cmp = LLVM.BuildICmp(builder, LLVMIntPredicate.LLVMIntSGT, lval, rval, "cgt");
                        else if (lvalType.TypeKind == LLVMTypeKind.LLVMFloatTypeKind)
                            cmp = LLVM.BuildFCmp(builder, LLVMRealPredicate.LLVMRealOGT, lval, rval, "cgt");
                        else if (lvalType.TypeKind == LLVMTypeKind.LLVMDoubleTypeKind)
                            cmp = LLVM.BuildFCmp(builder, LLVMRealPredicate.LLVMRealUGT, lval, rval, "cgt");
                        else
                        {
                            throw new NotImplementedException(
                                $"No comparision supported for those types: {lval} < {rval}");
                        }

                        builderStack.Push(new BuilderStackItem(MiniBCL.Int32Type, cmp));
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ceq)
                    {
                        var rval = builderStack.Pop().ValRef.Value;
                        var lval = builderStack.Pop().ValRef.Value;
                        var rvalType = rval.TypeOf();
                        var lvalType = lval.TypeOf();
                        if (!rvalType.Equals(lvalType))
                        {
                            rval = AutoCast(builder, rval, lvalType);
                        }

                        LLVMValueRef cmp;
                        if (lvalType.TypeKind == LLVMTypeKind.LLVMIntegerTypeKind)
                            cmp = LLVM.BuildICmp(builder, LLVMIntPredicate.LLVMIntEQ, lval, rval, "ceq");
                        else if (lvalType.TypeKind == LLVMTypeKind.LLVMFloatTypeKind)
                            cmp = LLVM.BuildFCmp(builder, LLVMRealPredicate.LLVMRealOEQ, lval, rval, "ceq");
                        else if (lvalType.TypeKind == LLVMTypeKind.LLVMDoubleTypeKind)
                            cmp = LLVM.BuildFCmp(builder, LLVMRealPredicate.LLVMRealOEQ, lval, rval, "ceq");
                        else
                        {
                            throw new NotImplementedException(
                                $"No comparision supported for those types: {lval} == {rval}");
                        }
                        builderStack.Push(new BuilderStackItem(MiniBCL.Int32Type, cmp));
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Br ||
                        instruction.OpCode == OpCodes.Br_S)
                    {
                        var operand = (Instruction) instruction.Operand;
                        var branchToBlock = branchTo[operand];
                        LLVM.BuildBr(builder, branchToBlock);
                        Debug(
                            $"[{instruction.OpCode} {operand}] -> Conditionally Redirect Control Context to Branch, terminating this block.");
                        break;
                    }

                    void AddBlockToProcess()
                    {
                        if (processedBranch.Contains(instruction.Next))
                        {
                            branchTo.Add(instruction.Next, entryBlock);
                            branchToProcess.Push(
                                new KeyValuePair<Instruction, LLVMBasicBlockRef>(instruction.Next, entryBlock));
                        }
                    }

                    if (instruction.OpCode == OpCodes.Beq ||
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
                        LLVMIntPredicate intCmp;

                        if (instruction.OpCode == OpCodes.Beq ||
                            instruction.OpCode == OpCodes.Beq_S)
                            intCmp = LLVMIntPredicate.LLVMIntEQ;
                        else if (instruction.OpCode == OpCodes.Bne_Un ||
                                 instruction.OpCode == OpCodes.Bne_Un_S)
                            intCmp = LLVMIntPredicate.LLVMIntNE;
                        else if (instruction.OpCode == OpCodes.Bge ||
                                 instruction.OpCode == OpCodes.Bge_S)
                            intCmp = LLVMIntPredicate.LLVMIntSGE;
                        else if (instruction.OpCode == OpCodes.Bge_Un ||
                                 instruction.OpCode == OpCodes.Bge_Un_S)
                            intCmp = LLVMIntPredicate.LLVMIntUGE;
                        else if (instruction.OpCode == OpCodes.Bgt ||
                                 instruction.OpCode == OpCodes.Bgt_S)
                            intCmp = LLVMIntPredicate.LLVMIntSGT;
                        else if (instruction.OpCode == OpCodes.Bgt_Un ||
                                 instruction.OpCode == OpCodes.Bgt_Un_S)
                            intCmp = LLVMIntPredicate.LLVMIntUGT;
                        else if (instruction.OpCode == OpCodes.Ble ||
                                 instruction.OpCode == OpCodes.Ble_S)
                            intCmp = LLVMIntPredicate.LLVMIntSLE;
                        else if (instruction.OpCode == OpCodes.Ble_Un ||
                                 instruction.OpCode == OpCodes.Ble_Un_S)
                            intCmp = LLVMIntPredicate.LLVMIntULE;
                        else if (instruction.OpCode == OpCodes.Blt ||
                                 instruction.OpCode == OpCodes.Blt_S)
                            intCmp = LLVMIntPredicate.LLVMIntSLT;
                        else if (instruction.OpCode == OpCodes.Blt_Un ||
                                 instruction.OpCode == OpCodes.Blt_Un_S)
                            intCmp = LLVMIntPredicate.LLVMIntULT;
                        else
                        {
                            // You have to somehow corrupt the program to get here.
                            throw new BadImageFormatException();
                        }
                        
                        var rval = builderStack.Pop();
                        var lval = builderStack.Pop();
                        var operand = (Instruction) instruction.Operand;
                        var branchToBlock = branchTo[operand];
                        entryBlock = LLVM.AppendBasicBlock(entryFunction, "branch");

                        var cmp = LLVM.BuildICmp(builder, intCmp, lval.ValRef.Value,
                            rval.ValRef.Value, instruction.OpCode.ToString());

                        LLVM.BuildCondBr(builder,
                            cmp,
                            entryBlock, branchToBlock);
                        
                        if (branchTo.ContainsKey(instruction.Next))
                            break;
                        
                        LLVM.PositionBuilderAtEnd(builder, entryBlock);
                        AddBlockToProcess();

                        Debug(
                            $"[{instruction.OpCode} {operand}] -> Popped {lval} and {rval} and pushed {cmp} and branched to {branchTo}");
                        continue;
                    }
                    
                    
                    if (instruction.OpCode == OpCodes.Brfalse ||
                        instruction.OpCode == OpCodes.Brfalse_S ||
                        instruction.OpCode == OpCodes.Brtrue ||
                        instruction.OpCode == OpCodes.Brtrue_S)
                    {
                        var lval = builderStack.Pop();
                        var operand = (Instruction) instruction.Operand;
                        var branchToBlock = branchTo[operand];
                        entryBlock = LLVM.AppendBasicBlock(entryFunction, "branch");
                        LLVMValueRef rval;
                        if (instruction.OpCode == OpCodes.Brtrue ||
                            instruction.OpCode == OpCodes.Brtrue_S)
                            rval = LLVM.ConstInt(lval.ValRef.Value.TypeOf(), 1, new LLVMBool(0));
                        else
                            rval = LLVM.ConstInt(lval.ValRef.Value.TypeOf(), 0, new LLVMBool(0));

                        var cmp = LLVM.BuildICmp(builder, LLVMIntPredicate.LLVMIntEQ, lval.ValRef.Value,
                            rval, instruction.OpCode.ToString());

                        LLVM.BuildCondBr(builder,
                            cmp,
                            entryBlock, branchToBlock);
                        if (branchTo.ContainsKey(instruction.Next))
                            break;
                        else
                        {
                            LLVM.PositionBuilderAtEnd(builder, entryBlock);
                            AddBlockToProcess();
                        }

                        Debug(
                            $"[{instruction.OpCode} {operand}] -> Created Branch");
                        continue;
                    }

                    if (instruction.OpCode == OpCodes.Ret)
                    {
                        if (method.ReturnType.FullName == "System.Void")
                        {
                            LLVM.BuildRetVoid(builder);
                            Debug($"[Ret {method.ReturnType.FullName}] -> Build Return.");
                            break;
                        }

                        var val = builderStack.Pop();
                        var ret = LLVM.BuildRet(builder, val.ValRef.Value);
                        Debug(
                            $"[Ret {method.ReturnType.FullName}] -> Popped {val.ValRef.Value} from Stack and Build Return with {ret}");
                        break;
                    }

                    Debug($"Unhandled Opcode: {instruction.OpCode.ToString()}");
                    throw new Exception($"Unhandled Opcode: {instruction.OpCode.ToString()}");
                } while (instruction.Next != null);
            }

            var first = method.Body.Instructions.First();
            ProcessInstructions(first);
            processedBranch.Add(first);
            while (branchToProcess.Count > 0)
            {
                var item = branchToProcess.Pop();
                var terminator = item.Value.GetBasicBlockTerminator();
                if (terminator.Pointer != IntPtr.Zero)
                    continue;
                LLVM.PositionBuilderAtEnd(builder, item.Value);
                entryBlock = item.Value;
                ProcessInstructions(item.Key);
                processedBranch.Add(item.Key);
            }

            entryFunction.Dump();
            if (LLVM.VerifyFunction(entryFunction, LLVMVerifierFailureAction.LLVMPrintMessageAction) == new LLVMBool(1))
            {
                throw new Exception("Function is not well formed!");
            }

            SymbolToCallableFunction.Add(entryFunctionSymbol, entryFunction);
        }

        static bool IsTypeAnInteger(TypeReference reference)
        {
            if (reference.FullName == "System.SByte") return true;
            if (reference.FullName == "System.Byte") return true;
            if (reference.FullName == "System.Int16") return true;
            if (reference.FullName == "System.UInt16") return true;
            if (reference.FullName == "System.Int32") return true;
            if (reference.FullName == "System.UInt32") return true;
            if (reference.FullName == "System.Int64") return true;
            if (reference.FullName == "System.UInt64") return true;

            return false;
        }

        static bool IsTypeARealNumber(TypeReference reference)
        {
            if (reference.FullName == "System.Float") return true;
            if (reference.FullName == "System.Double") return true;
            return false;
        }

        private void DebugLLVMEmit(LLVMBuilderRef builder, string format, LLVMValueRef val)
        {
            var formatStr = LLVM.BuildGlobalStringPtr(builder, format, "");
            LLVM.BuildCall(builder, SymbolToCallableFunction["System.Console::WriteLine"],
                new LLVMValueRef[] {formatStr, val}, "");
        }

        private void LinkJIT()
        {
            clrLogger.Debug(
                LLVM.VerifyModule(llvmModuleRef, LLVMVerifierFailureAction.LLVMPrintMessageAction, out var error) !=
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
                LLVM.RunPassManager(pass, llvmModuleRef);
        }

        private void Compile()
        {
            var options = new LLVMMCJITCompilerOptions {OptLevel = 3, CodeModel = LLVMCodeModel.LLVMCodeModelLarge};
            LLVM.InitializeMCJITCompilerOptions(options);
            if (LLVM.CreateMCJITCompilerForModule(out llvmEngineRef, llvmModuleRef, options, out var error) !=
                new LLVMBool(0))
            {
                clrLogger.Debug($"Error: {error}");
            }
        }

        private void RunEntryFunction()
        {
            var entrySymbol = GetCSToLLVMSymbolName(assembly.MainModule.EntryPoint);
            var funcPtr = LLVM.GetPointerToGlobal(llvmEngineRef, SymbolToCallableFunction[entrySymbol]);
            var mainFunc = Marshal.GetDelegateForFunctionPointer<EntryFunc_dt>(funcPtr);
            mainFunc();
        }

        private static string GetCSToLLVMSymbolName(MethodReference method) =>
            $"{method.DeclaringType.FullName}::{method.Name}";
    }
}