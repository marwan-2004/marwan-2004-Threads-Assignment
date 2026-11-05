# Assignment 02 — Answers

Student name:  marwan maged
Group / ID: group #4



## 1\. Initial Runs (Before Fix)

* **Run 1:** Booked = 26, TablesLeft = 4
* **Run 2:** Booked = 5, TablesLeft = 1
* **Run 3:** Booked = 1, TablesLeft = 4

## What is wrong

\-A Race Condition occurs because 40 threads execute the Book method concurrently without thread synchronization
-Delay Impact: During Thread.Sleep(20), many threads enter the critical code area before any thread decrements TablesLeft, leading to Overbooking (negative table counts and total booked > 5

## How you fixed it

\-Introduced a private object \_lock and wrapped the checking and modification logic inside a lock (\_lock) block
-The lock ensures that only one thread at a time can execute the booking logic

## 2\. Final Runs (After Fix)

* **Run 1:** Booked = 5, TablesLeft = 0
* **Run 2:** Booked = 5, TablesLeft = 0
* **Run 3:** Booked = 5, TablesLeft = 0

