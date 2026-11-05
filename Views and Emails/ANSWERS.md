# Assignment 05 — Answers

Student name:  marwan maged 
Group / ID: Group # 4

## Failures you observed

-1. **Exceptions & Dictionary Corruption:** Occasional `NullReferenceException`, `KeyNotFoundException`, or `ArgumentException` occurred during `RecordView` due to concurrent structural modifications on the non-thread-safe `Dictionary`.
2. **Lost View Counts:** Even when exceptions didn't crash the application, the actual view count was far lower than 50,000 due to race conditions during increments.
3. **Queue Exceptions & Duplicate Sends:** `InvalidOperationException` was thrown inside `EmailWorker` when two worker threads attempted to `Dequeue()` from a single remaining element in the non-thread-safe `Queue`.

## What you changed

-1. **View Counting Path:** Replaced `Dictionary<string, int>` with `ConcurrentDictionary<string, int>` and utilized `Views.AddOrUpdate(product, 1, (_, current) => current + 1)`. This guarantees atomic insert or update operations per key.
2. **Email Job Path:** Replaced `Queue<string>` with `ConcurrentQueue<string>` and utilized `EmailJobs.TryDequeue(out string? job)` instead of unsafe `Count` checks followed by `Dequeue()`. This atomic operation safely retrieves items across multiple competing worker threads.

## Collection safety vs business-rule safety

-Making a data collection thread-safe (like `ConcurrentQueue` or `ConcurrentDictionary`) only protects the **internal data structure integrity** (preventing memory corruption or internal exceptions)[cite: 8]. 

However, it **does not automatically enforce multi-step business logic constraints**[cite: 8]:
* **Non-Atomic Operations Sequences:** Performing two separate thread-safe operations in sequence (e.g., checking `if (!dict.ContainsKey(key))` and then executing `dict.TryAdd(key, value)`) leaves a race condition gap between the two calls where another thread can alter state.
* **Domain State Consistency:** Business rules often span multiple entities or collections (e.g., deducting user balance and creating an email job). Thread-safe collections cannot coordinate operations across different objects atomically. Higher-level synchronization (locks, transactions, or domain orchestration) is still required.
