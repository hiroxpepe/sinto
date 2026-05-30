# Coding Standards

Derived from analysis of the MeowziQ source code.
Applies to all future OSS projects (Signo, Quyno, etc.) under MIT license.

---

## 1. File Header

MIT license header on every file. No exceptions.

```csharp
// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
```

Every class and interface carries the author tag in its XML doc:

```csharp
/// <author>h.adachi (STUDIO MeowToon)</author>
```

---

## 2. `using` Declaration Order

1. `System.*` (alphabetical)
2. Blank line
3. Third-party libraries (alphabetical)
4. Blank line
5. Own namespaces (alphabetical)
6. Blank line
7. `using static` (own namespaces last)

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

using Signo.Core.Audio;

using static Signo.Core.Env;
```

---

## 3. Section Divider Blocks

Outer class (indent level 1) — 95 slash characters:

```csharp
        ///////////////////////////////////////////////////////////////////////////////////////////////
        // Fields
```

Inner class (indent level 2) — 93 slash characters:

```csharp
            ///////////////////////////////////////////////////////////////////////////////////////////
            // Fields
```

One blank line after the divider line, before the first member.

---

## 4. Section Names and Order

Sections appear in this exact order. Omit sections that have no members.

```
// Const [nouns]
// static Fields [nouns, noun phrases]
// static Fields                          ← abbreviated form also acceptable
// static Constructor
// static Events
// static Properties [noun, adjective]
// public static Properties [noun, adjective]
// private static Properties [noun, adjective]
// Fields
// Constructor
// Events
// Properties [noun, adjective]
// public Methods [verb]
// public static Methods [verb]
// private Properties [noun, adjective]
// private Methods [verb]
// private static Methods [verb]
// inner Classes
```

**Rule: static before instance. public before private.**

---

## 5. Naming Conventions

| Member | Convention | Example |
|--------|-----------|---------|
| Class / Interface / Property / Event | `PascalCase` | `NoteItem`, `ApplyNote` |
| Method (public and private) | `camelCase` | `convertPattern`, `loadJson` |
| Field | `_snake_case` | `_note_item`, `_phrase_list` |
| Local variable / Parameter | `snake_case` | `note_item`, `start_tick` |
| Constant | `UPPER_SNAKE_CASE` | `MIDI_TRACK_COUNT` |

Note: method names are `camelCase` (not `PascalCase`) — this is intentional and project-specific.

---

## 6. Access Modifier Rules

- Fields: always write `private` explicitly (do not rely on implicit default).
- Methods: always write access modifier explicitly.
- `private` methods use `camelCase` — same as public methods.

```csharp
private Note _note;
private int _percussion_note_num;

private static Core.Pattern convertPattern(Pattern pattern) { ... }
```

---

## 7. Named Parameters

Always use named parameters at call sites when the argument type is not self-evident.

```csharp
new Core.Section(
    key: Key.Enum.Parse(x.Key),
    key_mode: Mode.Enum.Parse(x.Mode),
    pattern_list: x.PatternArray.Select(...).ToList()
)
```

---

## 8. Property Style

Single-expression: use `=>`.  
Multi-expression or side-effect setter: use block body.

```csharp
// single expression
public Chord Chord { get => _chord; }
public MidiChannel MidiCh { set => _midi_ch = (int)value; }

// multi-expression
public string Type {
    get => _type;
    set {
        _type = value;
        _track = new(type: _type);
    }
}
```

---

## 9. XML Documentation

Every public member has a `/// <summary>` block.  
Multi-point remarks use `<list type="bullet">`.  
Custom tags in use: `<author>`, `<todo>`, `<note>`.

```csharp
/// <summary>
/// Creates and applies Note objects based on the given parameters.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Handles both note and chord input.</item>
/// <item>Applies octave range for chord inversions.</item>
/// </list>
/// </remarks>
/// <todo>
/// Support single note version of applyRange.
/// </todo>
public void ApplyNote(...) { ... }
```

---

## 10. Multiple Constructors

When a class requires different initialization paths, use overloaded constructors (not optional parameters).  
Each constructor covers exactly one use case with a clear doc comment.

```csharp
/// <summary>Creates a parameter for note notation.</summary>
public Param(Note note, Exp exp, DataType type, bool auto_note = true) { ... }

/// <summary>Creates a parameter for chord notation.</summary>
public Param(Chord chord, Exp exp, DataType type) { ... }

/// <summary>Creates a parameter for drum beat notation.</summary>
public Param(Note note, int percussion_note_num, Exp exp, DataType type) { ... }
```

---

## 11. Inner Classes

Placed at the bottom of the outer class under `// inner Classes`.  
Used to encapsulate JSON deserialization targets (`[DataContract]` etc.) and tightly scoped helpers.  
Inner classes follow the same section ordering with 93-character dividers.

