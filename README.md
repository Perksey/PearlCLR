# PearlCLR
Unmanaged/Managed CLR Project in C#

Discord Server Invite: https://discord.gg/Ac5dPVn

Created by Tyler Crandall

Supports:

1. Basic structs/classes
2. Integer/Float Conversions and Operations
3. Console.WriteLine (treated as Printf for simplicity)
4. Support Branching such as For Loop, While Loop, If Else and so forth.

Does not support:

1. Generic (Work in progress)
2. Decimal Type (Currently using Floating Point 128 bit under IEEE 754 spec)
3. Object Type and it's metadata

# Project Roots:

## PearlCLR

The CLR Project itself which includes runtime/jit compilation code.

## PearlCLRExtensions

A project containing variety of functions and PearlCLR specific features that include, but not limited to, manual memory management and
type resolution and metadata management.

## Demo

A scratchpad project written in C# that imports PearlCLRExtensions project for PearlCLR specific features.

## PearlCLRRunner

A project that simply import PearlCLR and Demo and run PearlCLR on compiled Demo library.

# How it work?

The PearlCLR project simply takes in compiled .Net assembly and translate MSIL to LLVM IR. Though it haven't be implemented yet, the intended end-goal is to embed JIT Compiler/CLR with the compiled LLVM IR as a separate LLVM bitcode file to support a variety of features that can't be compiled ahead of time.

The PearlCLR is currently bootstrap from Dotnet Core and will eventually compile itself and embed itself with the compiled application. The PearlCLR is more or less intended to be a "Low-level" CLR that effectively brings C# down to the low level like C/C++ and Rust. Some of those can already be demonstrated in Demo project.

# Frequently Asked Questions:

## Why do I get an error, "System.AccessViolationException: Attempted to read or write protected memory. This is often an indication that other memory is corrupt" on Windows?

This is usually a problem with LLVMSharp library, it is currently on the roadmap for PearCLR development to fix upstream to LLVMSharp or to create an alternative binding to LLVM-C API. LLVMSharp is known to work well on Linux platform.

## Why not contribute to LLILC instead?

There are two reasons:

1. This CLR/JIT project is implemented in completely C#, not in C++.
2. LLILC is a dead project, the last meaningful commit is in 2016 and any commits after that are mostly about packaging and documentation. https://github.com/dotnet/llilc/commits/master

## It doesn't create an executable when I run it

It doesn't, at least not yet, it however does provide MainModule.bc which is a LLVM IR bitcode file that you can use in place of C/C++ source files in clang compilation. This however requires that you install the LLVM and Clang toolchain which can be found in https://llvm.org/

`clang -oProgram.exe MainModule.bc` would produce Program.exe from the provided LLVM IR.
