# Assignment 04 — Answers

Student name:  marwan maged
Group / ID: group # 4

## expected and actual output before change  
-  ex: 400000  actual : 197064

## Why Expected and Actual disagree

- Due to Data Race during concurrent updates to the `Views` variable.
- The  C# operation `Views++` is **non-atomic**. It compiles into three distinct CPU instructions:
1. **Read** the current value from memory.
2. **Increment** the value in a CPU register.
3. **Write** the updated value back to memory. operation `Views++` is **non-atomic**. It compiles into three distinct CPU instructions:
1. **Read** the current value from memory.
2. **Increment** the value in a CPU register.
3. **Write** the updated value back to memory.

## What you changed

-`Interlocked.Increment(ref _views)` was used inside `RegisterView()`.

## When would you protect the update differently

-I would protect updates differently using a `lock` or `SemaphoreSlim` in the following scenarios:
1. **Complex / Multi-variable State Updates:** If updating the view count required updating multiple related fields atomically (e.g., updating both `TotalViews` and logging the view event timestamp).
2. **Business Rule Checks:** If a conditional check depended on the value before incrementing (e.g., checking if maximum views allowed is reached before incrementing).
3. **Complex Data Structures:** If tracking views per user in a Dictionary or updating domain entity models instead of a single integer.


-expected = 400000
-actual = 400000