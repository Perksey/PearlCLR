# PearlCLR
An unmanaged/Managed CLR project in C#

[![Chat on Discord](https://img.shields.io/badge/chat-on%20discord-7289DA.svg)](https://discord.gg/e9BgByh)

Supports:

* Basic structs/classes
* Integer/Float Conversions and Operations
* Console.WriteLine (treated as Printf for simplicity)
* Support Branching such as For Loop, While Loop, If Else and so forth.

Does not support:

* Generic (Work in progress)
* Decimal Type (Currently using Floating Point 128 bit under IEEE 754 spec)
* Object Type and it's metadata

# Projects:

## PearlCLR

The CLR project itself which includes runtime/JIT compilation code.

## PearlCLRExtensions

A project containing variety of functions and PearlCLR specific features including, but not limited to, manual memory management and type resolution and metadata management.

## Demo

A scratchpad project written in C# that imports PearlCLRExtensions project for PearlCLR specific features.

## PearlCLRRunner

A project that simply imports PearlCLR and Demo and runs PearlCLR on the compiled Demo library.

# How does it work?

The PearlCLR project simply takes a compiled .NET assembly and translates MSIL to LLVM IR. Though it hasn't be implemented yet, the intended end-goal is to embed a JIT Compiler/CLR with the compiled LLVM IR as a separate LLVM bytecode file to support a variety of features that can't be compiled ahead of time.

PearlCLR currently bootstraps from .NET Core and will eventually embed itself into the compiled application. PearlCLR is intended to be a "Low-Level" CLR that effectively brings C# down to the levels of C/C++ and Rust; some of which are already be demonstrated in the Demo project.

# Frequently Asked Questions:

## Why do I get an AccessViolationException on Windows?

This is usually a problem with LLVMSharp; it's currently on the roadmap for PearCLR development to fix this issue upstream or to create an alternative binding to LLVM-C API. LLVMSharp is known to work well on Linux platforms.

## Why not contribute to LLILC instead?

There are two reasons:

1. This CLR/JIT project is implemented in completely C#, not in C++.
2. LLILC is a dead project, the last meaningful commit is in 2016 and any commits after that are mostly about packaging and documentation. https://github.com/dotnet/llilc/commits/master

## It doesn't create an executable when I run it

It doesn't, at least not yet. It does, however, provide MainModule.bc which is a LLVM IR bytecode file that you can use in place of C/C++ source files for `clang` compilation. Note that this does require the LLVM and Clang toolchain which can be found at https://llvm.org/

`clang -o Program.exe MainModule.bc` would produce Program.exe from the provided LLVM IR.
