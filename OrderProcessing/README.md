## Phase 1 Questions & Answers

### 1. Why are the sync and async times almost the same when orders are awaited one after another?
When you write `await` inside a loop sequentially (e.g., `foreach (var order in orders) { await ValidateOrderAsync(order); }`), the execution flow stops and waits for each order task to complete before moving to the next iteration. 
Even though the code uses asynchronous non-blocking methods, **awaiting tasks sequentially forces sequential execution**. Therefore, the total latency equals the sum of delays for all 20 orders, matching the synchronous execution time.

### 2. Is the thread ID before an await always the same as after it? Why? Use your own log lines as evidence.
**No, the thread ID before and after an `await` is NOT guaranteed to be the same**. 
When an `await` yields execution during an asynchronous operation (like `Task.Delay` or asynchronous file I/O), the original thread is freed back to the .NET `ThreadPool`. When the operation completes, the continuation is scheduled on any available thread in the thread pool.

**Evidence from console logs:**
[21:51:29.203] [Thread 7] Customer Repository -> Opening connection for Customer #1...
[21:51:29.412] [Thread 7] Customer Repository -> Executing query for Customer #1...
[21:51:29.828] [Thread 7] Customer Repository -> Customer #1 found (Ahmed Ali)
[21:51:29.829] [Thread 7] Product Repository -> Opening connection for Product #1...
[21:51:30.040] [Thread 7] Product Repository -> Executing query for Product #1...
[21:51:30.454] [Thread 11] Product Repository -> Opening connection for Product #3...


### 3 What does the thread do while Task.Delay() is waiting? And while Thread.Sleep() is waiting?
Thread.Sleep(): Blocks the thread completely. The thread remains assigned to
the operation, consumes OS stack resources (~1 MB), and sits idle doing zero productive work until the sleep timer expires.

Task.Delay(): Releases the thread immediately. It registers an OS timer event and returns the thread back to the ThreadPool
so it can execute other concurrent tasks[cite: 13, 16]. Once the timer triggers, a thread is picked from the pool to resume execution[cite: 13, 16].

### 4.  Why should a repository method return Task instead of T?
Returning Task<T> (or Task for void methods) allows the caller to execute the method asynchronously. It signals that the operation involves non-blocking asynchronous work (such as database access or network I/O).
This enables the caller to either await the result asynchronously or combine multiple repository tasks together (e.g., via Task.WhenAll) for concurrent processing.

### 5. What is the difference between File.ReadAllText and File.ReadAllTextAsync? Does the async version occupy a thread while the disk is reading?
File.ReadAllText: Synchronous file read. It blocks the calling thread while the operating system reads data from the physical disk driver.
File.ReadAllTextAsync: Asynchronous file read.




## Phase 2 Questions & Answers

### 1. What is the difference between starting a task and awaiting it?
* **Starting a task:** Instantiates and begins executing an asynchronous operation asynchronously. The thread continues executing the subsequent lines of code immediately without blocking[cite: 19].
* **Awaiting a task (`await`):** Pauses the execution flow of the calling `async` method until the task completes[cite: 19]. It yields the thread back to the pool while waiting and unwraps the result or throws exceptions if the task fails[cite: 13, 19].

---

### 2. Does every Task create a new thread? Does Task.Delay() use a thread while waiting?
* **No, every `Task` does NOT create a new OS thread[cite: 19].** Tasks are lightweight abstractions representing asynchronous work managed by the .NET `ThreadPool`[cite: 13, 19].
* **No, `Task.Delay()` does NOT use a thread while waiting[cite: 19].** It registers a timer callback on the OS timer queue and frees the calling thread immediately[cite: 13, 16].

---

### 3. What happens if you call .Result or .Wait() on an unfinished task instead of await? Why is it dangerous?
Calling `.Result` or `.Wait()` forces a synchronous, blocking wait on an asynchronous task[cite: 3, 19]. 
**Why it is dangerous:**
1. It blocks the current thread entirely until completion, defeating the purpose of async I/O[cite: 3, 16, 19].
2. It can cause **deadlocks** (especially in UI or ASP.NET contexts) when the task continuation attempts to marshal back to the blocked thread context.
3. Exceptions get wrapped inside an `AggregateException`, making exception handling cumbersome[cite: 19, 27].

---

### 4. What happens to an exception thrown inside a task that is never awaited?
If a task throws an exception and is never awaited, the exception goes unhandled by the caller[cite: 19]. It remains stored inside the `Task.Exception` property[cite: 19]. 
When the unawaited Task object gets garbage collected, the unhandled exception triggers the `TaskScheduler.UnobservedTaskException` event[cite: 19]. In older .NET runtimes, this could terminate the process.

---

### 5. When should you use Task.Run(), and when should you avoid it?
* **Use `Task.Run()`:** For **CPU-bound operations** (e.g., heavy math calculations, PDF/image processing) to offload intensive work from the main thread to a background thread pool thread[cite: 18, 19].
* **Avoid `Task.Run()`:** For **I/O-bound operations** (e.g., database queries, network requests, disk reads)[cite: 18, 19]. I/O operations already use asynchronous drivers and IOCP ports; wrapping them in `Task.Run()` wastes thread pool threads unnecessarily[cite: 16, 19].

---

### 6. What is the difference between Task and Task<T>?
* **`Task`:** Represents an asynchronous operation that returns no value (equivalent to an asynchronous `void`)[cite: 19].
* **`Task<T>`:** Represents an asynchronous operation that eventually returns a result value of type `T` upon completion[cite: 16, 19].

---

### 7. Why can a lock block not contain an await?
C# forbids `await` inside a `lock` block because C# `lock` constructs rely on thread affinity (`Monitor.Enter` and `Monitor.Exit` must be executed by the exact same OS thread)[cite: 6, 19]. 
Since an `await` can yield execution and resume on a completely different `ThreadPool` thread, calling `Monitor.Exit` on a different thread would throw a `SynchronizationLockException`[cite: 13, 16, 19].

---

### 8. Why did the unprotected stock reservation oversell, and how did you prevent it?
* **Why it oversold:** Two concurrent tasks read `StockQuantity = 1` simultaneously before either had written back the decremented value[cite: 17, 18, 19]. Both passed the stock availability check and decremented the stock, leaving stock at -1[cite: 17, 18, 19].
* **Prevention:** Replaced unprotected file writes with `JsonFileStore` thread synchronization (`SemaphoreSlim` / atomicity) inside `ReserveStockAsync`, ensuring read, check, and update operations complete as an atomic critical section[cite: 9, 10, 18].


## Phase 3 Questions & Answers

### 1. Task 9.6: Sync vs Async Comparison Answers
* **(1) Does async make one single order's workflow faster?**
  No. Asynchronous execution does not speed up an individual operation. Latency for a single order remains the sum of its database, gateway, and email delays.
* **(2) Why does async scale better for many orders?**
  Async frees threads back to the thread pool while waiting for I/O operations (timer delays, file access, network calls). This allows a small pool of threads to handle thousands of concurrent orders without consuming excessive OS threads and memory stacks.
* **(3) What happens to the sync version with 1,000 orders?**
  The sync version would either exhaust thread pool resources, spin up 1,000 OS threads (consuming over 1 GB of RAM just for thread stacks), or suffer severe thread contention and context-switching overhead, causing latency to skyrocket or crashing the system.
* **(4) Why is async essential for apps that call databases, files, and APIs?**
  Database calls, file system I/O, and external APIs spend most of their time waiting for network sockets or disk hardware to respond. Async ensures application threads stay free to process other incoming work during these idle waiting windows.

---

### 2. Task 9.7: Fire-and-Forget Investigation
* **When await is necessary:** Required whenever you need to ensure an operation completes before continuing, when you need the return value of a Task, or when exceptions thrown by the operation must be caught and handled properly.
* **When fire-and-forget is acceptable (if ever):** Fire-and-forget is generally unsafe. It is only acceptable in non-critical background jobs (e.g., non-vital telemetries/analytics) where failure has zero business impact, provided exceptions are explicitly handled internally using a safety wrapper.
* **Why `async void` is dangerous:** 
  1. Exceptions thrown inside `async void` methods cannot be caught by outer `try/catch` blocks.
  2. Unhandled exceptions in `async void` directly crash the entire application process.
  3. Callers cannot await an `async void` method to know when it finishes.
* **Why `async void` is acceptable for event handlers:** Event handler signatures in .NET expect a `void` return type (e.g., `EventHandler(object sender, EventArgs e)`). The language runtime specifically allows `async void` for UI/Control event subscriptions.



## Phase 4 Questions & Answers

### 1. Choice of Tools for Tasks 10.1 - 10.4
* **Task 10.1 (Live progress):** Used **`Task.WhenEach`** (or `Task.WhenAny` loop). "Start and await one by one" forces waiting in order of array index (Order #1, then #2, etc.), blocking progress output until earlier tasks finish regardless of which finished first.
* **Task 10.2 (Customer Dashboard):** Used **`Task.WhenAll`**. Awaiting one by one would execute the 4 database loads sequentially (~1500 ms total), whereas `Task.WhenAll` runs them concurrently, finishing in the time of the single slowest query (~600 ms).
* **Task 10.3 (Fastest Shipping Quote):** Used **`Task.WhenAny`** with a `CancellationTokenSource`. Awaiting one by one forces waiting for slow or failing carriers, while `Task.WhenAny` returns immediately as soon as the fastest valid quote arrives and cancels the rest.
* **Task 10.4 A (Fraud Scoring):** Used **`Parallel.For`**. Awaiting one by one or standard task loops overheads CPU threads; `Parallel.For` partitions CPU-bound loop iterations efficiently across CPU cores.
* **Task 10.4 B (Email Re-send):** Used **`Parallel.ForEachAsync`** with `MaxDegreeOfParallelism = 4`. Awaiting one by one processes emails sequentially (30 seconds total); `Parallel.ForEachAsync` throttles concurrency strictly to 4 concurrent tasks without needing manual semaphores.

---

### 2. In 10.2, how do you get every exception? What does a plain await give you?
* **Plain `await task`:** Only unwraps and throws the **first exception** that occurred, ignoring all subsequent exceptions.
* **Getting every exception:** Read `task.Exception` (or catch `AggregateException`). The `Exception.InnerExceptions` property holds the complete list of all exceptions thrown across all concurrent tasks.

---

### 3. In 10.3, what happens to the losing carriers if you never cancel them?
If losing carriers are not canceled, they continue running silently in the background until completion, wasting CPU cycles, memory, thread pool resources, and network bandwidth.

---

### 4. Why does 10.4 A scale with CPU cores while 10.4 B does not?
* **10.4 A (Fraud Scoring):** Is **CPU-bound**. The math loop actively executes instructions on CPU registers. More physical CPU cores allow more instructions to execute simultaneously in parallel.
* **10.4 B (Email Re-send):** Is **I/O-bound**. Threads spend 99% of their time idle waiting for network delays (`Task.Delay`). Increasing CPU cores provides no benefit because the limitation is the network latency and the concurrency limit (4).

---

### 5. Difference between Parallel Work (10.4 A) and Concurrent Asynchronous Work (10.1)
* **Parallel Work (10.4 A):** Executes multiple threads **simultaneously across multiple physical CPU cores** to perform heavy computations.
* **Concurrent Asynchronous Work (10.1):** Manages multiple ongoing I/O tasks on a **single or small number of threads** by yielding execution during waiting periods.

---

### 6. Cancelled Run (--cancel-after) Demonstration
When running with `--cancel-after 4000`:
* Orders currently in-flight receive the cancellation signal.
* In-progress tasks stop at their next `ct` check point.
* Reserved stock for incomplete orders is automatically rolled back via `ReleaseStockAsync`.
* Any completed payments are refunded, and the order is saved with `Status = Pending` and `FailureReason = "Cancelled"`.
* JSON files remain 100% valid with zero data corruption.