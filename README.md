# T1 Bootstrap Compiler

This is the first prototype of an interpreter/compiler for T1. It is
written in C#. Its purpose is to get replaced with a new, better
compiler written in T1, that will first be compiled (when it actually
exists) with this bootstrap compiler.

Code here is experimental, ugly, and incomplete. The following parts
are implemented and mostly work:

  - Base T1 interpreter.
  - Whole-program type analysis.
  - Function merger for code generation.
  - Generator for type declaration and constant instances.

These parts need to be written to complete the bootstrap compiler:

  - Threaded code generator.
  - Object layout descriptors (for initialization and GC).
  - Garbage collector (optional at this stage: we could use
    [Hans Boehm's GC](https://www.hboehm.info/gc/)).
  - Basic stdlib functions for reading and writing files, growable
    lists, and sorted maps.

Once the real T1 compiler is written, the code in the bootstrap compiler
will be abandoned (i.e. kept around for historical reference).
