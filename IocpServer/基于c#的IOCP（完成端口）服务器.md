# 基于c#的IOCP（完成端口）服务器

##  一、完成端口（IOCP）

  ### 1、介绍  

​       IOCP全称I/O Completion Port，中文译为I/O完成端口。IOCP是一个异步I/O的Windows API，它可以高效地将I/O事件通知给应用程序

​        IOCP模型属于一种通讯模型，适用于Windows平台下高负载服务器的一个技术。在处理大量用户并发请求时，如果采用一个用户一个线程的方式那将造成CPU在这成千上万的线程间进行切换，后果是不可想象的。而IOCP完成端口模型则完全不会如此处理，它的理论是并行的线程数量必须有一个上限-也就是说同时发出500个客户请求，不应该允许出现500个可运行的线程。目前来说，IOCP完成端口是Windows下性能最好的I/O模型，同时它也是最复杂的内核对象。它避免了大量用户并发时原有模型采用的方式，极大的提高了程序的并行处理能力。

### 2、原理

​        ![原理图](.\iocp\img\iocp原理.png)

​        一共包括三部分：完成端口（存放重叠的I/O请求），客户端请求的处理，等待者线程队列（一定数量的工作者线程，一般采用CPU*2个）

　　完成端口中所谓的[端口]并不是我们在TCP/IP中所提到的端口，可以说是完全没有关系。它其实就是一个通知队列，由操作系统把已经完成的重叠I/O请求的通知放入其中。当某项I/O操作一旦完成，某个可以对该操作结果进行处理的工作者线程就会收到一则通知。

　　通常情况下，我们会在创建一定数量的工作者线程来处理这些通知，也就是**线程池**的方法。线程数量取决于应用程序的特定需要。理想的情况是，线程数量等于处理器的数量，不过这也要求任何线程都不应该执行诸如同步读写、等待事件通知等阻塞型的操作，以免线程阻塞。每个线程都将分到一定的CPU时间，在此期间该线程可以运行，然后另一个线程将分到一个时间片并开始执行。如果某个线程执行了阻塞型的操作，操作系统将剥夺其未使用的剩余时间片并让其它线程开始执行。也就是说，前一个线程没有充分使用其时间片，当发生这样的情况时，应用程序应该准备其它线程来充分利用这些时间片。

### 3、IOCP优点

​        完成端口会充分利用**<u>Windows内核来进行I/O的调度，是用于C/S通信模式中性能最好的网络通信模型。</u>**

​        IOCP通信模型具有高性能，高并发的特点：

​        （1）主要采用了异步I/O操作，弥补了同步操作线程阻塞耗时的缺点。

​        （2）采用线程池进行处理，减少了因为Thread创建，切换上下文占用过多的系统资源。让这几个线程等着，等到有用户请求来到的时候，就把这些请求都加入到一个公共消息队列中去，然后这几个开好的线程就排队逐一去从消息队列中取出消息并加以处理，这种方式就很优雅的实现了异步通信和负载均衡的问题，因为它提供了一种机制来使用几个线程“公平的”处理来自于多个客户端的输入/输出，并且线程如果没事干的时候也会被系统挂起，不会占用CPU周期。

​        （3）采用重叠I/O技术，帮助维持可以重复使用内存池。重叠模型是让应用程序使用重叠数据结构(WSAOVERLAPPED)，一次投递一个或多个Winsock I/O请求。针对这些提交的请求，在它们完成之后，应用程序会收到通知，于是就可以通过自己另外的代码来处理这些数据了。

### 4、基于c#完成端口通信模型的实现

​          c#封装了两种方式，通过线程池的处理实现完成端口模型，不需要面对操作系统的完成端口内核。

​        （1）beginxxx   endxxx方式：beginxxx发起异步操作连接，endxxx在回调函数中调用，完成具体的异步操作代码。

​        （2）封装SocketAsyncEventArgs类实现完成端口通信。在以前工作项目中采用第一种方式，本次，重点说明SocketAsyncEventArgs类如何实现高性能通信。

> MSDN对SocketAsyncEventArgs类的注解：
>
> [SocketAsyncEventArgs](https://docs.microsoft.com/zh-cn/dotnet/api/system.net.sockets.socketasynceventargs?view=net-5.0)类是[System.Net.Sockets.Socket](https://docs.microsoft.com/zh-cn/dotnet/api/system.net.sockets.socket?view=net-5.0) 类的一组增强功能的一部分，这些增强功能提供了一 种可供专用高性能套接字应用程序使用的替代异步模式。 此类专为需要高性能的网络服务器应用程序而设计。 应用程序可以独占方式使用增强的异步模式，也可以仅在目标热区中使用 (例如，在接收大量数据) 时使用。
>
> 这些增强功能的主要功能是避免在大容量异步套接字 I/O 期间重复分配和同步对象。 当前由 [System.Net.Sockets.Socket](https://docs.microsoft.com/zh-cn/dotnet/api/system.net.sockets.socket?view=net-5.0) 类实现的***开始/结束设计模式***需要为 每个异步套接字操作分配一个对象 [System.IAsyncResult](https://docs.microsoft.com/zh-cn/dotnet/api/system.iasyncresult?view=net-5.0)。
>
> 在新的 [System.Net.Sockets.Socket](https://docs.microsoft.com/zh-cn/dotnet/api/system.net.sockets.socket?view=net-5.0) 类增强功能中，异步套接字操作由 [SocketAsyncEventArgs](https://docs.microsoft.com/zh-cn/dotnet/api/system.net.sockets.socketasynceventargs?view=net-5.0) 应用程序分配和维护的可重用对象进行描述。 高性能套接字应用程序非常清楚必须维持的重叠套接字操作的数量。 该应用程序可创建所需的 [SocketAsyncEventArgs](https://docs.microsoft.com/zh-cn/dotnet/api/system.net.sockets.socketasynceventargs?view=net-5.0) 对象数量。 例如，如果服务器应用程序在任何时候都需要有15个套接字接受操作来支持传入的客户端连接速率，则它可以分配15个可重复使用 [SocketAsyncEventArgs](https://docs.microsoft.com/zh-cn/dotnet/api/system.net.sockets.socketasynceventargs?view=net-5.0) 的对象来实现此目的。
>
> 使用此类执行异步套接字操作的模式包括以下步骤：
>
> 1. 分配一个新的 [SocketAsyncEventArgs](https://docs.microsoft.com/zh-cn/dotnet/api/system.net.sockets.socketasynceventargs?view=net-5.0) 上下文对象，或从应用程序池中获取一个空闲对象。
> 2. 将上下文对象的属性设置为要执行的操作， (完成回调方法、数据缓冲区、缓冲区中的偏移量和要传输的最大数据量，例如) 。
> 3. 调用适当的套接字方法 (xxxAsync) 以启动异步操作。
> 4. 如果 (xxxAsync) 的异步套接字方法返回 true，则在回调中查询上下文属性的完成状态。
> 5. 如果 (xxxAsync) 的异步套接字方法返回 false，则操作同步完成。 可查询上下文属性获取操作结果。
> 6. 重新使用上下文进行另一项操作，将其放回池中，或放弃它。
>
> 新的异步套接字操作上下文对象的生存期由应用程序代码和异步 i/o 引用的引用确定。 作为参数提交给异步套接字操作方法之一后，应用程序不必保留对异步套接字操作上下文对象的引用。 完成回调返回之前，应用程序会继续引用它。 但是，应用程序保留对上下文的引用是有利的，以便以后可以重复使用它进行异步套接字操作。

+ 创建acceptSocketAsyncEventArgs 对象，注册acceptSocketAsyncEventArgs 的Completed事件处理连接成功代码

+ 创建listenSocket监听套接字，绑定本地ip，端口

+ 调用listenSocket.AcceptAsync(acceptSocketAsyncEventArgs)接收客户端连接。

+ 连接成功后，取出连接成功socket。  Socket s = e.AcceptSocket;

+ 从SocketAsyncEventArgs对象池中取出一个空闲对象，用于读写操作SocketAsyncEventArgs asyniar = _objectPool.Pop();

+ 将连接成功的socket赋值到SocketAsyncEventArgs对象的UserToken中

  asyniar.UserToken = s;

+ 连接成功socket调用ReceiveAsync(asyniar)进行异步数据接收

+ 同时投递下一个连接请求

+ asyniar注册的Completed事件中处理接收的数据，socket循环调用ReceiveAsync接收数据

## 二、SocketAsyncEventArgs类

### 1、分配一个新的 SocketAsyncEventArgs 上下文对象，或者从应用程序池中获取一个空闲的此类对象。

```c#
SocketAsyncEventArgs saea = new SocketAsyncEventArgs();
//或者(这里的SocketAsyncEventArgsPool类一般是自己实现，MSDN有通过栈结构实现的程序池，也可以使用队列或链表)：
//从读写socket栈中取出一个SocketAsyncEventArgs对象，用于读写操作
SocketAsyncEventArgs asyniar = _objectPool.Pop();
```

### 2、将上下文对象的属性设置为要执行的操作， (完成回调方法、数据缓冲区、缓冲区中的偏移量和要传输的最大数据量，例如) 。

```c#
 _bufferManager = new BufferManager(_bufferSize*_maxClient*opsToPreAlloc,_bufferSize);//声明缓存
 _objectPool = new SocketAsyncEventArgsPool(_maxClient);//声明SocketAsyncEventArgs池子大小，用于从里面取出读写socket数据
_maxAcceptClient = new Semaphore(_maxClient, _maxClient);//信号量 声明可以并发连接的最大个数
```

### 3、调用适当的套接字方法 (xxxAsync) 以启动异步操作。

```C#
//如果 I/O 操作挂起，则为 true。 操作完成时，将引发 e 参数的 Completed 事件。
//如果 I/O 操作同步完成，则为 false。 将不会引发 e 参数的 Completed 事件，
//并且可能在方法调用返回后立即检查作为参数传递的 e 对象以检索操作的结果。
if (!listenSocket.AcceptAsync(asyniar))
{
   ProcessAccept(asyniar);
}
```

### 4、如果 (xxxAsync) 的异步套接字方法返回 true，则在回调中查询上下文属性的完成状态。如果 (xxxAsync) 的异步套接字方法返回 false，则操作同步完成。 可查询上下文属性获取操作结果。

```c#
socket.ConnectAsync(saea);     //异步进行连接
socket.AcceptAsync(saea);     //异步接收连接
socket.ReceiveAsync(saea);     //异步接收消息
socket.SendAsync(saea);     //异步发送消息
//这里注意的是，每个操作方法返回的是布尔值，这个布尔值的作用，是表明当前操作是否有等待I/O的情况，如果返回false则表示当前是同步操作，不需要等待，此时要要同步执行回调方法，一般写法是
bool willRaiseEvent = socket.ReceiveAsync(saea); //继续异步接收消息
if (!willRaiseEvent)
{
    MethodName(saea);
}
```

> MSDN对xxxAsync方法的解释：
>
> 如果 I/O 操作挂起，则为 `true`。 操作完成时，将引发 `e` 参数的 [Completed](https://docs.microsoft.com/zh-cn/dotnet/api/system.net.sockets.socketasynceventargs.completed?view=net-5.0) 事件。
>
> 如果 I/O 操作同步完成，则为 `false`。 将不会引发 `e` 参数的 [Completed](https://docs.microsoft.com/zh-cn/dotnet/api/system.net.sockets.socketasynceventargs.completed?view=net-5.0) 事件，并且可能在方法调用返回后立即检查作为参数传递的 `e` 对象以检索操作的结果。

### 5、将该上下文重用于另一个操作，将它放回到应用程序池中，或者将它丢弃。

　　如果用于持续监听连接，要注意 asyniar.AcceptSocket = null;只有把asyniar对象的AcceptSocket置为null，才能监听到新的连接；s
　　如果只用于单次通讯，则在用完asyniar对象是可丢弃，asyniar.Dispose(),如果想重复利用，则设置相应的异步操作即可，

```c#
//第一次连接，创建用于连接SocketAsyncEventArgs的对象，注册连接完成方法
if (asyniar == null)
{
    asyniar = new SocketAsyncEventArgs();
    asyniar.Completed += new EventHandler<SocketAsyncEventArgs>     (OnAcceptCompleted);
}
//不是第一次连接，将AcceptSocket=null，方便再次连接获取新的AcceptSocket对象
else
{
     asyniar.AcceptSocket = null;
}
//信号量，控制最大客户端连接个数
_maxAcceptClient.WaitOne();
//如果 I/O 操作挂起，则为 true。 操作完成时，将引发 e 参数的 Completed 事件。
//如果 I/O 操作同步完成，则为 false。 将不会引发 e 参数的 Completed 事件，
//并且可能在方法调用返回后立即检查作为参数传递的 e 对象以检索操作的结果。
if (!listenSocket.AcceptAsync(asyniar))
{
   ProcessAccept(asyniar);
} 
socket.ReceiveAsync(saea);//重新接收
socket.SendAsync(saea);//重新发送
```

### 6、SocketAsyncEventArgs.Completed 事件

> ## 注解
>
> [Completed](https://docs.microsoft.com/zh-cn/dotnet/api/system.net.sockets.socketasynceventargs.completed?view=net-5.0)事件为客户端应用程序提供了一种完成异步套接字操作的方法。 [SocketAsyncEventArgs](https://docs.microsoft.com/zh-cn/dotnet/api/system.net.sockets.socketasynceventargs?view=net-5.0)当异步套接字操作启动时，事件处理程序应附加到实例中的事件，否则应用程序将无法确定操作完成的时间。
>
> 事件引用的完成回调委托 [Completed](https://docs.microsoft.com/zh-cn/dotnet/api/system.net.sockets.socketasynceventargs.completed?view=net-5.0) 包含处理客户端的异步套接字操作的程序逻辑。
>
> 当事件收到信号时，应用程序将使用 [SocketAsyncEventArgs](https://docs.microsoft.com/zh-cn/dotnet/api/system.net.sockets.socketasynceventargs?view=net-5.0) object 参数获取已完成的异步套接字操作的状态。

## 三、Semaphore信号量

​       在实现中有应用到一个Semaphore信号量用于控制服务器连接的最大客户端数

> ### 注解	    	
>
> 线程通常使用 [WaitOne](https://docs.microsoft.com/zh-cn/dotnet/api/system.threading.waithandle.waitone?view=net-5.0) 方法进入信号量，并且通常使用此方法重载来退出。
>
> 如果 [SemaphoreFullException](https://docs.microsoft.com/zh-cn/dotnet/api/system.threading.semaphorefullexception?view=net-5.0) [Release](https://docs.microsoft.com/zh-cn/dotnet/api/system.threading.semaphore.release?view=net-5.0) 方法引发，则不一定表示调用线程出现问题。 其他线程中的编程错误可能导致该线程退出信号量的次数超过了输入的时间。
>
> 如果当前的 [Semaphore](https://docs.microsoft.com/zh-cn/dotnet/api/system.threading.semaphore?view=net-5.0) 对象表示已命名的系统信号量，则用户必须具有 [SemaphoreRights.Modify](https://docs.microsoft.com/zh-cn/dotnet/api/system.security.accesscontrol.semaphorerights?view=net-5.0#System_Security_AccessControl_SemaphoreRights_Modify) 权限，并且必须已使用权限打开该信号量 [SemaphoreRights.Modify](https://docs.microsoft.com/zh-cn/dotnet/api/system.security.accesscontrol.semaphorerights?view=net-5.0#System_Security_AccessControl_SemaphoreRights_Modify) 。

```c#
using System;
using System.Threading;

public class Example
{
    // A semaphore that simulates a limited resource pool.
    //
    private static Semaphore _pool;

    // A padding interval to make the output more orderly.
    private static int _padding;

    public static void Main()
    {
        // Create a semaphore that can satisfy up to three
        // concurrent requests. Use an initial count of zero,
        // so that the entire semaphore count is initially
        // owned by the main program thread.
        //
        _pool = new Semaphore(0, 3);

        // Create and start five numbered threads. 
        //
        for (int i = 1; i <= 5; i++)
        {
            Thread t = new Thread(new ParameterizedThreadStart(Worker));

            // Start the thread, passing the number.
            //
            t.Start(i);
        }

        // Wait for half a second, to allow all the
        // threads to start and to block on the semaphore.
        //
        Thread.Sleep(5000);

        // The main thread starts out holding the entire
        // semaphore count. Calling Release(3) brings the 
        // semaphore count back to its maximum value, and
        // allows the waiting threads to enter the semaphore,
        // up to three at a time.
        //
        Console.WriteLine("Main thread calls Release(3).");
        _pool.Release(3);

        Console.WriteLine("Main thread exits.");
        Console.ReadKey();
    }

    private static void Worker(object num)
    {
        // Each worker thread begins by requesting the
        // semaphore.
        Console.WriteLine("Thread {0} begins " +
            "and waits for the semaphore.", num);
        _pool.WaitOne();

        // A padding interval to make the output more orderly.
        int padding = Interlocked.Add(ref _padding, 100);

        Console.WriteLine("Thread {0} enters the semaphore.  padding值：{1}", num, padding);

        // The thread's "work" consists of sleeping for 
        // about a second. Each thread "works" a little 
        // longer, just to make the output more orderly.
        //
        Thread.Sleep(1000 + padding);
       
        Console.WriteLine("Thread {0} releases the semaphore.", num);
        Console.WriteLine("Thread {0} previous semaphore count: {1}",
            num, _pool.Release());
       // int a = _pool.Release(;
    }
}
```

下面的代码示例创建一个信号量，其最大计数为3，初始计数为零。 该示例启动五个线程，这会阻止等待信号量。 主线程使用 [Release(Int32)](https://docs.microsoft.com/zh-cn/dotnet/api/system.threading.semaphore.release?view=net-5.0#System_Threading_Semaphore_Release_System_Int32_) 方法重载将信号量计数增加到其最大值，从而允许三个线程进入信号量。 每个线程使用 [Thread.Sleep](https://docs.microsoft.com/zh-cn/dotnet/api/system.threading.thread.sleep?view=net-5.0) 方法等待一秒，以模拟工作，然后调用 [Release()](https://docs.microsoft.com/zh-cn/dotnet/api/system.threading.semaphore.release?view=net-5.0#System_Threading_Semaphore_Release) 方法重载以释放信号量。 每次释放信号灯时，都将显示以前的信号量计数。 控制台消息跟踪信号量使用。 每个线程的模拟工作时间间隔略有增加，使输出更易于读取。

运行结果：

![运行结果](.\iocp\img\信号量运行结果.JPG)

结果分析：

信号量最大并发是3，初始是0，开启5个线程

waitone进入信号量，计数减1，release释放信号量，计数加1

初始0，线程阻塞，5s后，Realease（3）

Realease退出信号量并返回前一个计数

线程1,3,2进入信号量

| 线程号      | 信号量计数     |
| ----------- | -------------- |
| 1进入信号量 | 2              |
| 3进入信号量 | 1              |
| 2进入信号量 | 0              |
| 1释放信号量 | 1 前一计数值 0 |
| 4进入信号量 | 0              |
| 3释放信号量 | 1前一计数值 0  |
| 5进入信号量 | 0              |
| 2释放信号量 | 1前一计数值 0  |
| 4释放信号量 | 2前一计数值 1  |
| 5释放信号量 | 3前一计数值 2  |

## 四、IDisposable接口标准实现

C#里可以嵌入非托管代码，这就涉及到了这些代码资源的释放。以前总是看到别人的代码里那么写，也没有好好想想为什么，今天看了书，总结一下。

资源释放分为两种：

1. 托管的
2. 非托管的

两者的释放方式不一致：

1. 没有非托管资源的，GC在运行时，会自动回收和释放；
2. 含有非托管资源的，必须提供一个析构器，他们也会在内存里停留的时间会更长，最终被加入一个叫做finalization queue的结构，然后由GC在另一个线程释放；

实现IDispose接口是一种标准的释放资源的方式，正确使用会减少很多的bug和因为资源释放而引起的问题。正如上面所说，包含了非托管资源的代码，是必须提供一个析构器的。这样做的目的，是为了保证类的使用者，即使没有显式地调用Dispose方法，也可以正确地释放资源。

释放资源的Dispose方法，应该完成以下几件事（引用Effective C#）

1. 释放所有的非托管资源；
2. 释放所有的托管资源；
3. 设定一个标志，标志资源是否已经销毁；对于销毁的对象，仍旧调用，则应抛异常；
4. 跳过终结操作，调用GC.SuppressFinalize(this)方法。

```c#
public class DisposablClass : IDisposable
{
    //是否回收完毕
    bool _disposed;
    public void Dispose()
    {
        Dispose(true);    
        GC.SuppressFinalize(this);
    }
    ~DisposableClass()
    {
        Dispose(false);
    }
    //这里的参数表示示是否需要释放那些实现IDisposable接口的托管对象
    protected virtual void Dispose(bool disposing)
    {
        if(_disposed) return; //如果已经被回收，就中断执行
        if(disposing)
        {
            //TODO:释放那些实现IDisposable接口的托管对象
        }
        //TODO:释放非托管资源，设置对象为null
        _disposed = true;
    }
}
```

**Dispose()方法**

当需要回收非托管资源的DisposableClass类，就调用Dispoase()方法。而这个方法不会被CLR自动调用，需要手动调用。

**DisposableClass()，析构函数**

当托管堆上的对象没有被其它对象引用，GC会在回收对象之前，调用对象的析构函数。这里的~DisposableClass()析构函数的意义在于告诉GC你可以回收我，Dispose(false)表示在GC回收的时候，就不需要手动回收了。

**虚方法Dispose(bool disposing)**

1、通过此方法，所有的托管和非托管资源都能被回收。参数disposing表示是否需要释放那些实现IDisposable接口的托管对象。

2、如果disposings设置为true，就表示DisposablClass类依赖某些实现了IDisposable接口的托管对象，可以通过这里的Dispose(bool disposing)方法调用这些托管对象的Dispose()方法进行回收。

3、如果disposings设置为false,就表示DisposableClass类依赖某些没有实现IDisposable的非托管资源，那就把这些非托管资源对象设置为null，等待GC调用DisposableClass类的析构函数，把这些非托管资源进行回收。

4、另外，以上把Dispose(bool disposing)方法设置为protected virtual的原因是希望有子类可以一起参与到垃圾回收逻辑的设计，而且还不会影响到基类。

## 五、参考资料

### 1、IOCP介绍

https://www.cnblogs.com/xiaobingqianrui/p/9258665.html

https://blog.csdn.net/piggyxp/article/details/6922277

https://blog.csdn.net/neicole/article/details/7549497/

https://blog.csdn.net/weixin_33979363/article/details/86037868

### 2、SocketAsyncEventArgs类

https://docs.microsoft.com/zh-cn/dotnet/api/system.net.sockets.socketasynceventargs?view=net-5.0

https://www.cnblogs.com/supheart/p/4284500.html

### 3、 完成端口的实现

https://www.cnblogs.com/tuyile006/p/10980391.html

https://blog.csdn.net/qq_31967569/article/details/81221570

https://blog.csdn.net/qq_31967569/article/details/81221601

https://www.cnblogs.com/yaosj/p/11170244.html

https://segmentfault.com/a/1190000003834832

https://blog.csdn.net/zhujunxxxxx/article/details/43573879/

### 4、Semaphore信号量

https://docs.microsoft.com/zh-cn/dotnet/api/system.threading.semaphore?view=net-5.0

### 5、IDisposable接口

https://www.cnblogs.com/tiancai/p/6612444.html

https://www.cnblogs.com/warnet/p/5084504.html











