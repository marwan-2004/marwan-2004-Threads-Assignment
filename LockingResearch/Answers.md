Student name: marwan maged
Group / ID: Group #4

1.Provides a mechanism that synchronizes access to objects  & helps us to avoid the race condition problem by protecting the critical section.

2.The lock statement acquires the mutual-exclusion lock for a given object, executes a statement block, and then releases the lock. While a lock is held, the thread that holds the lock can acquire and release the lock multiple times.
Any other thread is blocked from acquiring the lock and waits until the lock is released. The lock statement ensures that at most only one thread executes its body at any moment in time.

3.   A monitor associates the data to be protected and the mutual exclusion and synchronization primitives required to protect accesses to the data.
	Monitors is a programming-language construct that does the same thing as semiphores/locks, but Monitors control the shared data by synchronizing at run time. In contrast, locks protect the shared data by just "spinning" which can lead to poor CPU utilization

4.Complex Synchronization: When the code requires more control over the synchronization mechanism ,
Advanced Multi-Threading Scenarios: When the code involves complex multi-threading scenarios where the developer needs to manage the state of the monitor and the threads effectively,
Thread-Switching: When the code involves thread-switching while holding a lock, which can lead to deadlock or other issues,
Pulse and Wait: When the code needs to signal the completion of a task or the end of a critical section.

5.   lock:  
	 private readonly object _lockObj=new();
	 private int _counter=0;
	 public void Increament()
	{
	  lock(_lockObj)
	  {
	    _counter++;
	  }
	}



	monitor: 
	private readonly object _lockObj = new();
private int _counter = 0;

public void Increment()
{
    bool lockTaken = false;
    try
    {
        Monitor.Enter(_lockObj, ref lockTaken);
        _counter++;
    }
    finally
    {
        if (lockTaken)
        {
            Monitor.Exit(_lockObj);
        }
    }
}






links : 
https://www.c-sharpcorner.com/article/introduction-to-monitor-class-in-c-sharp
https://learn.microsoft.com/en-us/dotnet/api/system.threading.monitor?view=net-10.0

