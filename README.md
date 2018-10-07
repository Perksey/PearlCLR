# PearlCLR
Unmanaged/Managed CLR Project in C#

Created by Tyler Crandall

Supports:

1. Basic structs/classes
2. Integer/Float Conversions and Operations
3. Console.WriteLine (treated as Printf for simplicity)
4. Support Branching such as For Loop, While Loop, If Else and so forth.

Does not support:

1. Generic (Work in progress)
2. Decimal Type
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

# Frequently Asked Questions:

## Why do I get an error, "System.AccessViolationException: Attempted to read or write protected memory. This is often an indication that other memory is corrupt" on Windows?

This is a problem with LLVMSharp library, it is currently on the roadmap for PearCLR development to fix upstream to LLVMSharp or to create an alternative binding to LLVM-C API. LLVMSharp is known to work on Linux platform.
