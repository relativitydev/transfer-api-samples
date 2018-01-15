# Relativity Transfer API for .NET
You can use the Transfer API (TAPI) to build application components that connect to Relativity and stream data from external sources into Relativity storage using different transfer protocols, for example, HTTP, SMB, and Aspera. You can also stream data from Relativity. The API enables optimized data transfer with extensible client architecture and event model using Relativity authentication and logging. For example, you can use the Transfer API to develop an application that loads case data into Relativity for subsequent processing. Unlike the Import API, TAPI does not create Relativity objects associated with the data, for example, documents and RDOs.

***Important!**  We're providing TAPI as a preview release so that you can evaluate it and provide us with feedback. The preview release offers a functional API, which will continue to undergo minor contract changes and further enhancements.*

TAPI features include:
* Only asynchronous methods
* Thread-safe
* Transfers in a single request or by a request job
* Cancellation using CancellationToken
* Progress using context object
* Diagnostics (for example, compatibility check)
* Relativity user authentication
* Built-in RCC package/transfer support
* Logging with the Relativity logging framework and [Serilog](https://serilog.net/)

We are  providing a sample solution to help you get started developing your own transfer applications.

## Integrations
As of this writing, TAPI is now integrated within the following components and applications:

* RDC/WinEDDS/IAPI/RIP/Invariant
* EnForce (Guidance/OpenText)
* Relativity One Staging Explorer (ROSE)
* R1 Import via Azure Functions

## Target Framework
* .NET 4.6.2


## Dependencies
The following NuGet packages are required by the TAPI solution:

* aspera.faspnative (native Aspera engine)
* dtx.rcc.client32.win (32-bit RCC API)
* dtx.rcc.client64.win (64-bit RCC API)
* Newtonsoft.Json
* Polly
* ssh.net
* relativity.faspmanager (Aspera API)
* Relativity.Logging
* Relativity.Logging.Interfaces

***Note:** The RCC packages are only required when using the RCC package library API's.**


To obtain TAPI libraries, contact [Relativity support](mailto:support@relativity.com).

## Components
The Transfer API consists of the following key components:

* **Relativity.Transfer.IRelativityTransferHost** - the host where files are uploaded and downloaded.
* **Relativity.Transfer.ITransferClient** - the operations to perform transfers when the source paths **are** immediately known.
* **Relativity.Transfer.ITransferJob** - the job that performs file uploads and downloads when the source paths **are not** immediately known.
* **Relativity.Transfer.TransferContext** - the context for transfer request operations, including relevant events and basic configuration.
* **Relativity.Transfer.TransferPath** - the path that details the source, target, and optional filename properties.

## Supported transfer clients
The transfer API uses [MEF (Managed Extensibility Framework)](https://docs.microsoft.com/en-us/dotnet/framework/mef/) design to search and construct clients. Relativity supports the following clients:

* Aspera
* File Share
* HTTP

## Sample solution

The `Sample.sln` solution is an out-of-the-box template for developing your own custom transfer applications. The solution already includes all required references.

You can also run the solution to see a transfer in action. Prerequisites for running the solution:

* Visual Studio 2015
* A Relativity instance that you can connect to
* Valid Relativity credentials

Before running the solution, edit `Program.cs` to specify the Relativity URL, credentials, and workspace artifact ID. You can also specify the files to be transferred or make sure the hard-coded path (`C:\Datasets\sample.pdf`) exists on your machine. When you run the solution, the program displays detailed messages about the progress to the console. After the transfer completes, you can verify success by examining the Relativity file share directory.

The `Program.cs` logic is as follows:

We set up the application logging as the first step in the `Main()` method:
```csharp
try
    {
        // Setup global settings for all transfers.
        GlobalSettings.Instance.ApplicationName = "sample-app";

        // Don't be too aggressive with logging statistics.
        GlobalSettings.Instance.StatisticsLogEnabled = true;
        GlobalSettings.Instance.StatisticsLogIntervalSeconds = 2;

        // Any attempt to exceed this max will throw an exception.
        GlobalSettings.Instance.MaxAllowedTargetDataRateMbps = 100;

        // All temp files can be stored in a specific directory or UNC share.
        //// GlobalSettings.Instance.TempDirectory = @"C:\MyTemp";

        // If a transfer log isn't specified, Relativity Logging is always used. You can optionally construct the transfer log and pass the ILog instance too.
        //// using (ITransferLog transferLog = new RelativityTransferLog(logInstance))

        // Using a custom transfer log to send all entries to Serilog.
        using (ITransferLog transferLog = new CustomTransferLog())
        {
            ExecuteUploadDemo(transferLog);
        }
    }
    catch (Exception e)
    {
        Console.WriteLine("A serious transfer failure has occurred. Error: " + e);
    }
    finally
    {
        Console.ReadLine();
    }
}
```

Note the use of the of a custom `ITransferLog` object and the `ApplicationName` property to make the logs more user-friendly. We assume that in most cases you would be using your own preferred logging framework when writing custom transfer applications.

We then instantiate the ExecuteUploadDemo class:

```csharp
public static void Main(string[] args)
{
    // Suppress SSL validation errors.
    ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, errors) => true;

    try
    {
        ExecuteUploadDemo();
    }
    catch (Exception e)
    {
        Console.WriteLine("A serious transfer failure has occurred. Error: " + e);
    }
    finally
    {
       Console.ReadLine();
    }
}
```

`ExecuteUploadDemo` contains all the code illustrating the use of the Transfer API:

* [Subscribing to transfer events](#subscribing-to-transfer-events)
* [Cancellation](#cancellation)
* [Relativity host setup and authentication](#relativity-host-setup-and-authentication)
* [Creating a client](#creating-a-client)
* [Setting up a transfer request job](#setting-up-a-transfer-request-job)
* [Wait for job completion and results](#wait-for-job-completion-and-results)

### Subscribing to transfer events

`ExecuteUploadDemo` begins with instantiation of the `TransferContext` object. `TransferContext` is used to decouple the event logic of the transfer - for example, progress and  statistics - from the host and the client.  

```csharp
// The context object is used to decouple operations such as progress from other TAPI objects.
TransferContext context = new TransferContext { StatisticsRateSeconds = 2.0 };
```
You can set a number of options on the context object for subscribing to events:

```csharp
context.TransferPathIssue += (sender, args) =>
{
    Console.WriteLine($"*** The transfer error has occurred. Attributes={args.Issue.Attributes}");
};

context.TransferRequest += (sender, args) =>
{
    Console.WriteLine($"*** The transfer request {args.Request.ClientRequestId} status has changed. Status={args.Status}");
};

context.TransferPathProgress += (sender, args) =>
{
    if (args.Status == TransferPathStatus.Successful)
    {
        Console.WriteLine($"*** The source file '{args.Path.SourcePath}' transfer is successful.");
    }
};

context.TransferJobRetry += (sender, args) =>
{
    Console.WriteLine($"*** Job {args.Request.ClientRequestId} is retrying. Retry={args.Count}");
};

context.TransferStatistics += (sender, args) =>
{
    // Disabling since the GlobalSettings are already configured to write the transfer statistics to the console.
    // Console.WriteLine($"*** Progress: {args.Statistics.Progress:00.00}%, Transfer rate: {args.Statistics.TransferRateMbps:00.00} Mbps");
};
```

For more information, see [TransferContext](#transfercontext).

### Cancellation
We then create the cancellation token. The use of cancellation token logic is strongly recommended with potentially long-running transfer operations:

```csharp
// The CancellationTokenSource is used to cancel the transfer operation.
CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
//// cancellationTokenSource.Cancel();
```
### Relativity host setup and authentication
Next, we construct the Relativity host object and credentials. This is where you must specify the Relativity URL, credentials, and workspace artifact ID before running the solution:

```csharp
// TODO: Update with the Relativity instance, credentials, and workspace.
Uri relativityHost = new Uri("https://relativity_host.com/Relativity");
IHttpCredential credential = new BasicAuthenticationCredential("jsmith@example.com", "UnbreakableP@ssword777");
int workspaceId = 1027428;
```
TAPI supports basic authentication and OAuth2. For more information about Relativity OAuth2 clients, see {Relativity Documentation Site]("https://help.relativity.com/9.5/Content/Relativity/Authentication/OAuth2_clients.htm"). 

### Creating a client
We then create a transfer client using the `CreateClientAsync()` method and supply a custom `ClientConfiguration` to the transfer. Note the `using` statement with `CreateClientAsync()` (as well as other asynchronous TAPI operations that implement `IDisposable`):

```csharp
// The configuration object provides numerous options to customize the transfer.
ClientConfiguration configuration =
    new ClientConfiguration
        {
            PreCalculateJobSize = false,
            FileNotFoundErrorsRetry = false,
            PreserveDates = true
        };

// The CreateClientAsync method chooses the best client at runtime.  
using (IRelativityTransferHost host = new RelativityTransferHost(new RelativityConnectionInfo(relativityHost, credential, workspaceId)))
using (ITransferClient client = await host.CreateClientAsync(cancellationTokenSource.Token))
{
    // Display a friendly name for the client that was just created.
    Console.WriteLine($"Client {client.DisplayName} has been created.");
```
The `CreateClientAsync()` method queries the specified Relativity workspace for configured transfer methods (Aspera, file share, or HTTP) to determine which clients would be supported and selects the optimal method at runtime. Note that you can also use the ``CreateClient`` method to explicitly instruct TAPI to construct a certain type of client client, for example, if you know that FileShareClient will always be your best transfer option. For more information, see [Dynamic Transfer Client](#dynamic-transfer-client) and [ITransferClient](#itransferclient).

###  Setting up a transfer request job
Next, we retrieve Relativity workspace details to get the UNC path of the destination share, and use it to create the `TransferRequest` object:
```csharp
// Retrieve workspace details in order to specify a UNC path.
var workspace = await client.GetWorkspaceAsync(cancellationTokenSource.Token);
var targetPath = Path.Combine(workspace.DefaultFileShareUncPath + @"\UploadDataset");
TransferRequest uploadRequest = TransferRequest.ForUploadJob(targetPath, context);
```
Note that this is where we specify the `TransferContext` object created earlier as the optional parameter for the TransferRequest. 

In cases when the file paths are not known and you want to avoid calling `TransferAsync` one file at a time (as the paths are discovered), use the transfer via job (`TransferRequest.ForUploadJob()`). TAPI jobs provide a mechanism for controlling the lifecycle of the job and adding paths as they become known without incurring a performance hit. Transfer via a request (`TransferRequest.ForUpload()` method) can be used when the list of file paths to be transferred is already known. For more information, see [Transfer via request](#transfer-via-request) and [Transfer via job](#transfer-via-job).

### Wait for job completion and results
We then create a transfer job. After that, we add file paths to the asynchronous queue and perform immediate transfers:

```csharp
// Once the job is created, an asynchronous queue is available to add paths and perform immediate transfers.
using (var job = await client.CreateJobAsync(uploadRequest, cancellationTokenSource.Token))
{
    // Setup the upload request using 1 of 2 approaches.
    // 1. Use the SearchLocalPathsAsync API.
    // 2. Specify the precise files.

    //// TODO: Update with a search path.
    //const bool PreserveFolders = true;
    //var searchResults = await client.SearchLocalPathsAsync(
    //                        @"C:\Datasets\transfer-api-sample",
    //                        PreserveFolders,
    //                        targetPath,
    //                        cancellationTokenSource.Token);
    //job.AddPaths(searchResults.Paths);

    // TODO: Update this collection with valid source paths to upload.
    job.AddPaths(
    new[]
    {
        new TransferPath { SourcePath = @"C:\Datasets\sample.pdf" }
    });

    // Await completion of the job up to the specified max time period. Events will continue to provide feedback.
    ITransferResult uploadResult = await job.CompleteAsync(cancellationTokenSource.Token);
    Console.WriteLine($"Upload transfer result: {uploadResult.Status}, Files: {uploadResult.TotalTransferredFiles}");
    Console.WriteLine($"Upload transfer data rate: {uploadResult.TransferRateMbps:#.##} Mbps");
    Console.WriteLine("Press ENTER to terminate.");
}
```

This is where you can edit the path(s) of files to be transferred. Again, note the `using` statement when creating the job, and await-async pattern when performing the transfer with the `CompleteAsync()` method.

Finally, we write transfer results out to the console.

The following sections provide detailed reference for the Transfer API operations illustrated by the sample program above.

## Usage
The next sections cover TAPI usage including:

* [RelativityConnectionInfo](#relativityconnectioninfo)
* [RelativityTransferHost](#relativitytransferhost)
* [ClientConfiguration](#clientconfiguration)
* [ITransferClient](#itransferclient)
* [Dynamic Transfer Client](#dynamic-transfer-client)
* [ITransferClientStrategy](#itransferclientstrategy)
* [Client Support Check](#client-support-check)
* [Connection Check](#connection-check)
* [IRemotePathResolver](#iremotepathresolver)
* [IRetryStrategy](#iretrystrategy)
* [TransferPath](#transferpath)
* [TransferRequest](#transferrequest)
* [ITransferResult](#itransferresult)
* [TransferStatus](#transferstatus)
* [ITransferJob](#itransferjob)
* [Transfer via Request](#transfer-via-request)
* [Transfer via Job](#transfer-via-job)
* [Change Job Data Rate](#change-job-data-rate)
* [Transfer Events and Statistics](#transfer-events-and-statistics)
* [Error Handling and ITransferIssue](#error-handling-and-itransferissue)
* [Packaging and RCC Package Library](#packaging-and-rcc-package-library)
* [Global Settings](#global-settings)
* [Logging](#logging)
* [DateTime object values](#datetime-object-values)
* [Binding Redirect for NewtonSoft.Json](#binding-redirect-for-newtonsoftjson)

### RelativityConnectionInfo
The first thing you must do is construct a `RelativityConnectionInfo` object, which requires the following:

| Property        | Description |
| --------------- |-----------------------------------------------------------------------------------|
| Host            | The Relativity URL. |
| Credential      | The Relativity credential used to authenticate HTTP/REST API calls. |
| WorkspaceId     | The artifact identifier used to retrieve workspace specific transfer information. |

The following example uses basic username/password credentials.

```csharp
const int WorkspaceId = 111111;
var connectionInfo = new RelativityConnectionInfo(
    new Uri("http://localhost/Relativity"),
    new BasicAuthenticationCredential("relativity.admin@relativity.com", "MyUbreakablePassword777!"),
    WorkspaceId);
```
When using an OAUTH2 client to authenticate, the bearer token is provided instead.

```csharp
const int WorkspaceId = 111111;
var connectionInfo = new RelativityConnectionInfo(
    new Uri("http://localhost/Relativity"),
    new BearerTokenCredential(bearerToken),
    WorkspaceId);
```

### RelativityTransferHost
Given the `RelativityConnectionInfo` object, the `RelativityTransferHost` object is then constructed. This object implements `IDisposable` to manage object lifecycles and should employ a using block.

```csharp
using (IRelativityTransferHost host = new RelativityTransferHost(connectionInfo))
{ }
```

***Note:** This object implements the `IDisposable` interface to ensure the life-cycle is managed properly.*

### ClientConfiguration
Before you can create a client, you have to provide a `ClientConfiguration` instance. If you know which client you would like to construct, choose strongly-typed class object that derives from `ClientConfiguration`. As you might expect, there are a number client specific properties to control the client transfer behavior:

| Property                   | Description                                                                                                                                                                                                  | Default Value                      |
| ---------------------------| -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|------------------------------------|
| ClientId                   | The transfer client unique identifier.                                                                                                                                                                       | Guid.Empty                         |
| Client                     | The well-known transfer client.                                                                                                                                                                              | WellKnownTransferClient.Unassigned |
| CookieContainer            | The HTTP cookie container.                                                                                                                                                                                   | new instance                       |
| FileNotFoundErrorsDisabled | The value indicating whether to disable treating missing files as errors.                                                                                                                                    | false                              |
| FileNotFoundErrorsRetry    | The value indicating whether to retry missing file errors..                                                                                                                                                  | true                               |
| FileSystemChunkSize        | The size of chunks, in bytes, when transferring files over file-system transports.                                                                                                                           | 16KB                               |
| HttpChunkSize              | The size of chunks, in bytes, when transferring files over HTTP transports.                                                                                                                                  | 1MB                                |
| MaxHttpRetryAttempts       | The maximum number of retry attempts for a HTTP service call.                                                                                                                                                | 5                                  |
| MaxJobParallelism          | The maximum degree of parallelism when executing a job. This value specifies the number of threads used to transfer the paths contained within the job queue.                                                | 1                                  |
| MaxJobRetryAttempts        | The maximum number of job retry attempts.                                                                                                                                                                    | 3                                  |
| MinDataRateMbpsKey         | The minimum data rate in Mbps units. This setting isn't guaranteed to be used by all clients but considered a hint to clients that support targeted data rates.                                              | 0                                  |
| PreCalculateJobSize        | The value indicating whether the overall job size is pre-calculated to use more accurate byte-level progress and statistics. Care should be taken when using this setting on massive datasets.               | false                              |
| PreserveDates              | The value indicating whether file dates (metadata) are preserved.                                                                                                                                            | true                               |
| TargetDataRateMbps         | The target data rate in Mbps units. This setting isn't guaranteed to be used by all clients but considered a hint to clients that support targeted data rates.                                               | 100 Mbps                           |
| TimeoutSeconds             | The timeout. This value is typically used whenever executing a web-service.                                                                                                                                  | 300 seconds                        |
| TransferLogDirectory       | The directory where clients can store transfer logs. This setting isn't guaranteed to be used by all clients but provided if specialized transfer log files are available, independent of Relativity Logging.| null                               |
| ValidateSourcePaths        | The value indicating whether to validate source paths before they're added to the job queue. An `ArgumentException` is thrown when a validation failure occurs.                                            | true                               |

***Note:** API users are strongly encouraged to set the `TransferLogDirectory` because clients that support this feature typically write more detailed diagnostic information into their custom log files.*


### ITransferClient
The API caller uses the transfer host to construct the appropriate transfer client. This object implements `IDisposable` to manage client specific object lifecycles. **MEF** is used to dynamically construct the appropriate instance.

```csharp
// I need an Aspera client.
using (ITransferClient client = host.CreateClient(new AsperaClientConfiguration()))
{ }

// I need a file share client.
using (ITransferClient client = host.CreateClient(new FileShareClientConfiguration()))
{ }

// I need an HTTP client.
using (ITransferClient client = host.CreateClient(new HttpClientConfiguration()))
{ }
```

The construction can also be driven by a `IDictionary<string, string>` containing name/value pairs. This is ideal in situations where the decision on which client to construct and how to configure is dynamically driven.

```csharp
Dictionary<string, string> existingProperties;
using (ITransferClient client = host.CreateClient(new ClientConfiguration(existingProperties)))
{
}
```

***Note:** This object implements the `IDisposable` interface to ensure the life-cycle is managed properly.*


### Dynamic transfer client
In the above examples, it's understood that the configuration object specifies which client to construct. The `ClientId` property contained within the `ClientConfiguration` object provides MEF enough information to construct the appropriate client.

In some circumstances, you might not want to choose the client; rather, you would like TAPI to make the choice on your behalf. In this model, client metadata is retrieved via MEF and they're sorted from best to worst. Given a list of potential clients, TAPI runs a support check (see below) to determine if the client is fully supported in both Relativity and the client environment. Since this can take 10-15 seconds, the operation is asynchronous.

```csharp
using (ITransferClient client = await host.CreateClientAsync(new ClientConfiguration(existingProperties)))
{
}
```

***Note:** This object implements the `IDisposable` interface to ensure the life-cycle is managed properly.*

### ITransferClientStrategy

When using the dynamic transfer client, a strategy design pattern is used to determine the order in which clients are checked for compatibility. Out of the box, the **default strategy** is as follows:

* FileShare
* Aspera
* HTTP

The signature for this interface is as follows:

```csharp
interface ITransferClientStrategy
{
    IEnumerable<ITransferClientMetadata> GetSortedCandidates(RelativityConnectionInfo connectionInfo, IEnumerable<ITransferClientMetadata> availableClients);
}
```

The caller is provided with the Relativity instance and all discovered transfer client plugin metadata. The returned collection is then used to provide a sorted list of candidates to use.

### Client support check
Given a transfer client object, the `SupportCheckAsync` method is called to perform the following checks:

* Verify the client is supported by Relativity.
* Verify the client is supported within the client-side environment.

The following example verifies the file share associated with the configured workspace is defined and accessible.

```csharp
using (IRelativityTransferHost host = new RelativityTransferHost(connectionInfo))
using (ITransferClient client = host.CreateClient(new FileShareClientConfiguration()))
{
    ISupportCheckResult result = await client.SupportCheckAsync();
    Console.WriteLine($"Support result: {result.IsSupported}");
}
```

### Connection check
A connection check performs a small upload and download to not only verify connectivity but to ensure support for reading from and writing to the storage. Likewise, a context object is provided to retrieve status messages. The check can be done on a per-client basis or a single method to exercise connection checks for all available clients.

The following example verifies the file share associated with the configured workspace is defined and accessible.

```csharp
using (IRelativityTransferHost host = new RelativityTransferHost(connectionInfo))
using (ITransferClient client = host.CreateClient(new FileShareClientConfiguration()))
{
    DiagnosticsContext context = new DiagnosticsContext();
    context.DiagnosticMessage += (sender, args) => { Console.WriteLine($"Connection check event. Client: {args.ClientId}, Message: {args.Message}"); };

    // Perform a connection check only for the file share client.
    IConnectionCheckResult result = await client.ConnectionCheckAsync(context);
    Console.WriteLine($"Connection check result: {result.IsSuccessful}");

    // Perform a connection check for all available clients.
    foreach (IConnectionCheckResult result in await host.ConnectionChecksAsync(context))
    {
        Console.WriteLine($"Connection check result: {result.IsSuccessful}")
    }
}
```


### IRemotePathResolver
The `TransferRequest` object exposes optional `SourcePathResolver` and `TargetPathResolver` properties to take a given path and adapt it. The signature for this interface is as follows:

```csharp
interface IRemotePathResolver
{
    string ResolvePath(string path);
}
```

The primary use-case for path resolvers is adding backwards compatibility to a client. For example, the Aspera client employs resolvers to adapt UNC paths to native Aspera UNIX-style paths.

***Note:** The library provides **AsperaUncPathResolver** for API users that wish to use Aspera clients when using UNC paths exclusively. This **must** be specified when native Aspera paths are **NOT** used.*


### IRetryStrategy
The `TransferRequest` object exposes an optional `RetryStrategy` property to define a method that dictates how much time is spent in between retry attempts. The signature for this interface is as follows:

```csharp
interface IRetryStrategy
{
    Func<int, TimeSpan> Calculation { get; }
}
```

The TAPI library provides a `RetryStrategies` static class to provide two common strategies:

* **Exponential backoff** - This is the default strategy where each attempt waits longer by a power of 2.
* **Fixed Time** - This simply defines a constant wait period regardless of the number of attempts.

### TransferPath
This object encapsulates a local or remote path and can be found throughout the API. This is a convenient extensibility point to support different kinds of paths.

| Property        | Description |
| --------------- |--------------------------------------------------------------------------------------------------------------------------------------------------- |
| Data            | The **optional** dictionary data used to provide client-specific path information. This is null by default until the AddData method is called. |
| Direction       | The transfer direction. If not specified, this property is automatically updated with the same property defined in the transfer request. |
| Order           | The **optional** order. This value can be used by the client to track transfer paths once they have been successfully transferred.  |
| SourcePath      | The local (upload) or remote (download) path source. This is automatically updated if the SourcePathResolver is defined in the transfer request. |
| Tag             | The **optional** object allows storing any custom object on the transfer path. |
| TargetPath      | The **optional** target path. If not specified, this property is automatically updated with the same property defined in the transfer request. |
| TargetFileName  | The **optional** target filename. If not specified, this property is automatically updated with the same property defined in the transfer request. |

***Note:*** Both relative paths and paths transformed via `IRemotePathResolver` are automatically assigned to the `TransferPath` object.


### TransferRequest
There are several options to submit transfers and all revolve around the `TransferRequest` object.

| Property           | Description |
| ------------------ |------------------------------------------------------------------------------------------------------ |
| ClientRequestId    | The **optional** client specified transfer request unique identifier for the entire request. If not specified, a value is automatically assigned. |
| Context            | The **optional** transfer context to configure progress events and logging. |
| Direction          | The **optional** transfer direction (Upload or Download). This is a global setting which, if specified, automatically updates all `TransferPath` objects **if not already assigned**. |
| JobId              | The **optional** unique identifier for the current submission or job. If not specified, a value is automatically assigned. |
| Name               | The **optional** name associated with this request. For Aspera transfers, this value is attached to all reporting data and useful for identification and search purposes. |
| Paths              | The transfer path objects. This is ignored when using a transfer job. |
| RetryStrategy      | The **optional** IRetryStrategy instance to define the amount of time to wait in between each retry attempt. By default, an exponential backoff strategy is assigned. |
| SourcePathResolver | The **optional** resolver used to adapt source paths from one format to another. This is set to `NullPathResolver` by default. |
| Tag                | The **optional** object allows storing any custom object on the transfer request. |
| TargetPath         | The transfer target path. This is a global setting which, if specified, automatically updates all `TransferPath` objects **if not already assigned**. |
| TargetPathResolver | The **optional** resolver used to adapt target paths from one format to another. This is set to `NullPathResolver` by default. |


Although this object can be constructed directly, a number of static methods have been added to simplify construction using several overloads including:

* TransferRequest.ForDownload
* TransferRequest.ForDownloadJob
* TransferRequest.ForUpload
* TransferRequest.ForUploadJob


### ITransferResult
Once the transfer is complete, the `ITransferResult` object is returned and provides an overall summary.

| Property              | Description |
| --------------------- |------------------------------------------------------------- |
| Elapsed               | The total transfer elapsed time. | 
| EndTime               | The **local** transfer end time. |
| Issues                | The read-only list of registered transfer issues. |
| Request               | The request associated with the transfer result. |
| RetryCount            | The total number of job retry attempts. |
| StartTime             | The **local** transfer start time. |
| Statistics            | The read-only list of all statistics for the initial request and all subsequent retry attempts. |
| Status                | The transfer status. |
| TotalFailedFiles      | The total number of files that failed to transfer. |
| TotalFatalErrors      | The total number of fatal errors reported. |
| TotalFilesNotFound    | The total number of files not found. |
| TotalSkippedFiles     | The total number of files skipped. |
| TotalTransferredBytes | The total number of transferred bytes. |
| TotalTransferredFiles | The total number of transferred files. |
| TransferError         | The most important error from the list of registered issues. |
| TransferRateMbps      | The average file transfer rate, in Mbps units, for the entire request. |


### TransferStatus
This enumeration provides the overall transfer status.

| Name                | Description |
| ------------------- | ------------------------------------- |
| NotStarted          | The transfer has not started. |
| Failed              | The transfer contains one or more paths that failed to transfer. These errors are non-fatal and can be retried. |
| Fatal               | The transfer stopped due to a fatal error. |
| Successful          | The transfer is successful. |
| Canceled            | The transfer is canceled. |


### ITransferJob
Once a client is constructed, transfer jobs are created in one of two ways (see below). Once constructed, the job adheres to a common structure regardless of client. The job has several key responsibilities including:

* Provide a life-cycle for the entire transfer.
* Add paths to a job queue.
* Wait for all transfers to complete.
* Status and current statistics.
* Retry count.
* Retrieving a list of all processed job paths.

***Note:** This object implements the `IDisposable` interface to ensure the life-cycle is managed properly.*


### Transfer via request
If the **list of source paths is already known**, you simply construct the request object, call `TransferAsync`, and await the result.

```csharp
using (ITransferClient client = host.CreateClient(configuration))
{
    const string SourcePath = @"C:\temp\file.txt";
    const string TargetPath = @"\\files\T003";
    TransferRequest request = TransferRequest.ForUpload(SourcePath, TargetPath);
    ITransferResult result = await client.TransferAsync(request);
    Console.WriteLine($"Status: {result.Status}");
}
```


### Transfer via job
If the **list of source paths is unknown**, you want to avoid calling `TransferAsync` one file at a time. High-speed clients like Aspera require a significant amount of overhead to setup the transfer request and performance would suffer significantly. To address this scenario, the `ITransferJob` object can be constructed via the client instance. The general idea is to construct a job, continually add transfer paths to the queue as they become known, and then await completion.

It's understood that files are transferred as soon as they're added to the job queue.

```csharp
// dr = datareader
TransferRequest request = TransferRequest.ForUploadJob(TargetPath);
using (ITransferClient client = host.CreateClient(clientConfiguration))
using (ITransferJob job = await client.CreateJobAsync(request))
{
    // Paths are added to the job as they become known.
    while (dr.Read())
    {
        string file = dr.GetString(0);
        job.AddPath(new TransferPath { Direction = TransferDirection.Upload, SourcePath = file, TargetFileName = Guid.NewGuid().ToString() });
    }

    // Wait until all transfers have completed.
    ITransferResult result = await job.CompleteAsync();
    Console.WriteLine($"Status: {result.Status}");
}
```

***Note:** Multiple jobs can be created; however, bandwidth constraints can lead to transfer errors.*


### Change Job Data Rate (Aspera-Only)
The `ClientConfiguration` object supports setting a minimum and target data rate. There are situations where the API user would like to *change* the data rate at runtime. To facilitate this feature, the `ITransferJob` object allows each TAPI client to provide an implementation.

This feature is currently limited to the **Aspera client** and any attempt to call this method on a job/client that doesn't support setting or changing the data rate will throw `NotSupportedException`. To provide the API user a cleaner way to use this feature, the `IsDataRateChangedSupported` property is provided.

```csharp
using (ITransferJob job = await client.CreateJobAsync(request))
{
    if (job.IsDataRateChangeSupported)
    {
        job.ChangeDataRate(0, 200);
        Console.WriteLine($"Changed the data rate. Min=0 Mbps, Target=200 Mbps.");
    }
}
```


### Transfer events and statistics
All transfer events are exposed through the optional `TransferContext` object and include:

| Property                 | Description |
| ------------------------ |----------------------------------------------------------------------------------- |
| EndTime                  | The **local** end time of the transfer operation. |
| LargeFileProgress        | Occurs when a single large file progress has changed. |
| LargeFileProgressEnabled | The value indicating whether `LargeFileProgress` events are raised. This value is disabled by default. |
| StartTime                | The **local** start time of the transfer operation. |
| StatisticsEnabled        | The value indicating whether `TransferStatistics` events are raised. This value is enabled by default. |
| StatisticsRateSeconds    | The rate, in seconds, that statistics events are raised. This value is 2 seconds by default. |
| TransferPathProgress     | Occurs when the transfer path progress changed. |
| TransferPathIssue        | Occurs when an issue occurs transferring a path. |
| TransferJobRetry         | Occurs when a transfer job is retried. |
| TransferRequest          | Occurs when the overall transfer request is started or ended. |
| TransferStatistics       | Occurs to provide overall progress, transfer rate, and total byte/file count info. |

***Note:*** *The `ITransferRequest` object can take an optional `TransferContext` instance when event handling is required.*

```csharp
var context = new TransferContext();
context.LargeFileProgress += (sender, args) => { Console.WriteLine($"Large file progress event. Transfer path: {args.Path}, Chunk number: {args.ChunkNumber}, Total chunks: {args.TotalChunks}"); };
context.TransferPathProgress += (sender, args) => { Console.WriteLine($"Transfer path progress event. Transfer path: {args.Path}, Started: {args.StartTime}, Ended: {args.EndTime}"); };
context.TransferPathIssue += (sender, args) => { Console.WriteLine($"Transfer failure. Transfer path: {args.Issue.Path} Message: {args.Issue.Message}"); };
context.TransferJobRetry += (sender, args) => { Console.WriteLine("Transfer job retry. Count: {args.Count}"); };
context.TransferRequest += (sender, args) => { Console.WriteLine("Transfer request event. Status: {args.Status}"); };
context.TransferStatistics += (sender, args) => { Console.WriteLine($"Transfer statistics: {args.Statistics.Progress}"); };

// Pass the context when constructing a new request.
TransferRequest request = TransferRequest.ForUpload(SourcePath, TargetPath, context);
```

The `TransferStatistics` event is notable because it provides a wealth of useful runtime transfer info. The `TransferStatisticsEventArgs` class exposes an `ITransferStatistics` object and provides the following properties.

| Property                | Description |
| ---------------------   |-------------------------------------------------------- |
| AverageTransferRateMbps | The average transfer rate, in Mbps units, for the current job. |
| EndTime                 | The **local** end time of the current job. |
| Id                      | The auto-generated unique identifier for this instance. |
| JobError                | The value indicating whether a job-level error has occurred. |
| JobErrorCode            | The client-specific job error code. |
| JobErrorMessage         | The client-specific job error message. |
| Order                   | The zero-based order in which the statistics were created. This value is incremented with each retry. |
| PreCalcEndTime          | The **local** time when the pre-calculation ended. |
| PreCalcStartTime        | The **local** time when the pre-calculation started. |
| Progress                | The progress for the entire request, regardless of retry. Byte-level progress is used when the `ClientConfiguration.PreCalculateJobSize` or `ITransferPathService` is used to determine the complete inventory of files; otherwise, file-level progress is used. The range is between 0-100. |
| RemainingTime           | The estimated remaining transfer time. The TotalRequestBytes must be calculated via `ClientConfiguration.PreCalculateJobSize` or `ITransferClient.SearchLocalPathsAsync` to calculate and return a valid value. |
| Request                 | The request associated with this statistics instance. |
| RetryAttempt            | The current job retry attempt number. |
| StartTime               | The **local** start time of the current job. |
| TotalFailedFiles        | The total number of files that failed to transfer. |
| TotalFilesNotFound      | The total number of files not found for the current job. |
| TotalFatalErrors        | The total number of fatal errors reported for the current job. |
| TotalRequestBytes       | The total number of bytes contained within the request. |
| TotalRequestFiles       | The total number of files contained within the request. |
| TotalSkippedFiles       | The total number of skipped files for the current job. |
| TotalTransferredBytes   | The total number of bytes transferred at this moment in time. |
| TotalTransferredFiles   | The total number of transferred files at this moment in time. |
| TransferRateMbps        | The active transfer rate, in Mbps units, for the current job. |
| TransferTimeSeconds     | The total transfer time in seconds. |

***Notes:*** 

* *Not all transfer clients are guaranteed to supply all listed statistics.*
* *The rate at which statistics are raised is defined via StatisticsRateSeconds property defined on TransferContext.*


### Error handling and ITransferIssue
If a fatal exception occurs, such as `OutOfMemoryException`, `ThreadAbortException`, or a general connection exception, **the rule is simple: TAPI throws `TransferException`**. This must be handled by the API user.

When dealing with requests containing thousands or even millions of files, it is not enough to simply throw an exception when a file transfer error occurs. To support this type of error handling model, the `ITransferIssue` object encapsulates warning/error information. The API user can then decide how to handle these issues. It's understood that all issues are logged automatically.

The ITransferIssue object includes the following properties:

| Property         | Description |
| ---------------- |--------------------------------------------------------------------------------------------------------------------------------------- |
| Attributes       | The bitwise transfer issue attributes (for example, `Warning`, `Error`, `File`, `FileNotFound`) |
| Code             | The client specific-warning or error code associated with the error. |
| Index            | The issue order or index in which the error was added. |
| MaxRetryAttempts | The maximum number of retry attempts. |
| Message          | The message associated with this issue. |
| Path             | The `TransferPath` object associated with the issue. This value can be null if the failure is unrelated to transferring a specific file. |
| RetryAttempt     | The current job retry attempt number. |
| Timestamp        | The **local** time when the issue occurred. |

The `IssueAttributes` enumeration provides common issues that can be combined (for example, `FlagsAttribute`) and occur across any transfer client.

| Name                 | Description |
| -------------------- | -------------------------------------------------------------------------------------------------------------- |
| Authentication       | The issue is due to a client-specific authentication resource. |
| Canceled             | The issue is due to a cancellation request. |
| DirectoryNotFound    | The issue is due to a directory not found. |
| Error                | The issue is fatal and can cause the overall transfer to fail without a successful retry. |
| File                 | The issue is due to the source or target file. |
| FileNotFound         | The issue is due to the source or target file not found. |
| Io                   | The issue is due to a generic I/O error. |
| InvalidCharacters    | The issue is due to invalid characters in a path. |
| InvalidPath          | The issue is due to invalid source or target path. |
| Job                  | The issue is due to a problem with the underlying job or configuration. |
| Licensing            | The issue is due to licensing. |
| LongPath             | The issue is due to an unsupported client-side or server-side long path. |
| Overwrite            | The issue is due to a file that already exists but the transfer is configured to not overwrite existing files. |
| ReadWritePermissions | The issue is due to read or write permissions. |
| StorageReadWrite     | The issue is due to storage read or write errors. |
| StorageOutOfSpace    | The issue is due to the storage running out of space. |
| Timeout              | The issue is due to a connection or command timeout. |
| Warning              | The issue is a non-fatal warning. |


The following example demonstrates how issues are enumerated once the result is returned to the API caller. 

```csharp
TransferRequest request = TransferRequest.ForUpload(SourcePath, TargetPath);
ITransferResult result = await client.TransferAsync(request).ConfigureAwait(false);
foreach (ITransferIssue issue in result.Issues)
{
    if (issue.Attributes.HasFlag(IssueAttributes.Error))
    {
        Console.WriteLine($"File error: {issue.Path.SourcePath} - Message: {issue.Message}");
    }
    else if (issue.Attributes.HasFlag(IssueAttributes.Warning))
    {
        Console.WriteLine($"File warning: {issue.Path.SourcePath} - Message: {issue.Message}");
    }
}
```


### Packaging and RCC Package Library
There may be scenarios where the cost of transferring massive datasets consisting of small files is significantly higher than packaging them locally, transferring, and potentially extracting them on the server (extracting may not be necessary, depending on workflow). There may also be scenarios where the API user would like to alter file metadata, such as filename or file timestamps, before adding the native files to the package container.

TAPI extends the existing plugin-based design with a simple means to create or use package libraries through a familiar and consistent object model. Some of the key features include:
* MEF-based plugin design.
* Package dependencies are *only* required if packaging is used.
* Package files are transferred *as soon as* each package file is completed.
* Package events are exposed through a new package context object.
* **Limited to uploads only.**


#### RCC Package Library NuGet Packages
The TAPI repository includes two NuGet packages to provide RCC package library functionality:
* relativity.transfer.package.rcc32 (32-bit package library)
* relativity.transfer.package.rcc64 (64-bit package library)


#### Package configuration
Similar to the `ClientConfiguration` object, the `PackageConfiguration` object is used to setup the package request and includes:

| Name                 | Description |
| -------------------- |----------------------------------------------------------------------------------- |
| Compression          | Specify whether to compress the files. |
| DeletePackageFiles   | Specify whether to delete the package files after they've been transferred. |
| FileName             | The package file name. |
| MultiFileMaxBytes    | The maximum number of bytes per package file (IE think of multi-file ZIP). |
| Name                 | The package name. The RCC library uses this value to specify the `DataSource` value. |
| PackageLibrary       | The WellKnownPackageLibrary enumeration value. This specifies which package library to use. |
| PackageLibraryId     | The package library unique identifier. This must be assigned when using a third-party package library. |
| Password             | The package password. |


#### Package events
Similar to the `TransferContext` object, all package events are exposed through the optional `PackageContext` object and includes:

| Event                | Description |
| -------------------- |----------------------------------------------------------------------------------- |
| PackagingFile        | Occurs when a source file is ready to be added to a package. This event should be handled if the source file metadata must be changed. |
| PackageProgress      | Occurs when the package progress changes. |
| PackageCreated       | Occurs when a new transfer package is created. |


#### RCC Format Info
For those that may not be familiar with the RCC format, this was developed in the summer of 2015 to secure custodian-based collections. In a nutshell, it's similar to multi-file ZIP but offers a number of advantages including:
* The format is based on the SQLCipher port of SQLite.
* Cross platform support (Windows and OSX).
* Dependent on 32-bit and 64-bit **native** libraries.
* **The executing application MUST assign the x86/x64 platform target (IE project settings)**
* Full database AES-256 encryption in CBC mode via OpenSSL.
* You **MUST** specify a 16-character password.
* Custom metadata can be stored within the package.
* Extraction is done through a WPF-based application or RCC extraction API.
* Compression not yet supported.


#### RCC File Types
A standard RCC is composed of three file types where each file type is uniquely identified. The SQLCipher database format provides a feature where the first 16 Bytes can be specified.

| RCC File Type             | Description                                                                                                                                                                    |
| --------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| \{filename\}.rcc          | The master RCC database contains metadata and other lookup tables.                                                                                                             |
| \{filename\}-cf.rcc       | The collected file database contains file and item metadata.                                                                                                                   |
| \{filename\}-cf-xxxx.rcc  | The BLOB database contains the raw collected files. A filename scheme includes numbers to support multi-files once the configured maximum file length is exceeded.             |


#### Package Example
There are very few differences between a standard transfer request and one involving packaging. Ignoring package configuration and events, the main difference is calling `PackageTransferAsync` instead of `TransferAsync`.

```csharp
var packageConfiguration = new PackageConfiguration
    {
        Compression = false,
        DeletePackageFiles = true,
        FileName = "Sample.rcc",
        Name = "Sample Dataset",
        PackageLibrary = WellKnownPackageLibrary.Rcc,
        Password = "P@ssw0rd@1012345"
    };

    // Custom metadata can be added to the package.
    var packageMetadata = new Dictionary<string, object>();

    // The package context exposes events, similar to how the context is used throughout TAPI.
    var context = new PackageContext(packageConfiguration, packageMetadata);

    // This event is raised when a new package is created.
    context.PackageCreated += (sender, e) =>
        {
            Console.WriteLine($"Package {e.File} has been created.");
        };

    // This event is raised when package progress occurs.
    context.PackageProgress += (sender, e) =>
        {
            Console.WriteLine("Package progress: {0:#.##}%", e.Progress);
        };

    // This event is raised as package files are about to be get added to the package.
    context.PackagingFile += (sender, e) =>
        {
            e.SourceFileMetadata.CreationTime = DateTime.Today;
            e.SourceFileMetadata.LastAccessTime = DateTime.Today.Subtract(TimeSpan.FromDays(1));
            e.SourceFileMetadata.LastWriteTime = DateTime.Today.Subtract(TimeSpan.FromDays(2));
            e.SourceFileMetadata.FileName = "modified-" + e.SourceFileMetadata.FileName;
            e.SourceFileMetadata.ExtractDirectory = null;
            Console.WriteLine($"Adding source file '{e.SourceFileMetadata.File}' to package.");
        };

    // Simultaneously package the request and transfer all package files.
    var request = TransferRequest.ForUpload(
        new TransferPath { SourcePath = @"C:\MyDataset" },
        @"\\files\PackageUpload");
    var result = await client.PackageTransferAsync(request, context);
```


### Global Settings
A number of common but optional settings are exposed by the `GlobalSettings` singleton.

| Property                     | Description                                                                                                            | Default Value                  |
| -----------------------------|------------------------------------------------------------------------------------------------------------------------|--------------------------------|
| ApplicationName              | The name of the application. This value is prefixed within all log entries.                                            | TAPI                           |
| LogPackageSourceFiles        | Specifies whether to log all source files added to the package. If true, the overhead can degrade package performance. | false                          |
| MaxAllowedTargetDataRateMbps | The max target data rate, in Mbps units, allowed by the transfer API.                                                  | 600                            |
| PluginDirectory              | The directory where all plugins are located.                                                                           | Working directory              |
| PluginFileNameFilter         | The file name filter to limit which files are searched for plugins.                                                    | *.dll                          |
| PluginFileNameMatch          | The file name match expression to limit which files are searched for plugins.                                          | Relativity.Transfer            |
| PluginSearchOption           | The file search option used when searching for plugins.                                                                | SearchOption.TopDirectoryOnly  |
| PrecalcCheckIntervalSeconds  | The number of seconds the pre-calculation values are checked to determine whether the value has changed.               | 1.0                            |
| StatisticsLogEnabled         | Automatically log transfer statistics.                                                                                 | false                          |
| StatisticsLogIntervalSeconds | The interval, in seconds, that transer statistics are logged.                                                          | 2.0                            |
| StatisticsMaxSamples         | The maximum number of statistics transfer rate samples to add additional weight to the remaining time calculation.     | 8                              |
| TempDirectory                | The directory used for temp storage.                                                                                   | Current user profile temp path |

***Note:** API users are strongly encouraged to set `ApplicationName` because the value is included within all log entries.*


### Logging
TAPI supports Relativity Logging and, specifically, the `ILog` object. There may be scenarios, however, where 3rd party developers may wish to use their own logging framework. The `ITransferLog` interface is an extensibility point to address this possible use-case. Similar to other TAPI objects, this interface implements `IDisposable` to manage object lifecycles.

Relativity Logging is the default logging implementation if none is explicitly provided. The next example demonstrates how a Relativity Logging `ILog` object is specified when creating the `RelativityTransferHost` object.

```csharp
ILog log; // Retrieved or constructed via Relativity Logging.
using (ITransferLog log = new RelativityTransferLog(log))
using (IRelativityTransferHost host = new RelativityTransferHost(connectionInfo, log))
{}
```

***Notes:*** 

* *If an ITransferLog object isn't provided, `Relativity.Logging.Log.Logger` is used to retrieve the current Relativity Logging instance.*
* *If a serious error occurs attempting to retrieve the `ILog` or an exception is thrown performing Relativity Logging setup, the `NullTransferLog` is constructed to avoid unnecessary fatal errors.*


#### Log Formatting
When a transfer request is made, all log entries are prefixed with useful property values to improve log searching and filtering to a particular request. This includes the following values:

* ApplicationName (via `GlobalSettings`)
* ClientRequestId (via `ITransferRequest`)
* Direction (via `ITransferRequest`)

***Note:** The `ApplicationName` should be set to an appropriate value. The value defaults to TAPI if not specified.*


#### Log Entries and Templates
Relativity Logging uses [Serilog](https://serilog.net/) to format each log entry. Because Relativity Logging is the presumed default, the [Serilog DSL](https://github.com/serilog/serilog/wiki/Writing-Log-Events) is used throughout TAPI. When using a custom `ITransferLog` implementation, the message template and properties must be converted to a string; otherwise, an exception is thrown when calling the String.Format method. [This StackOverflow page](https://stackoverflow.com/questions/26875831/how-do-i-render-a-template-and-its-arguments-manually-in-serilog) provides an example on how to use the `MessageTemplateParser` to convert the Serilog message and properties to a properly formatted string.


### DateTime object values
All `DateTime` objects values used by TAPI are in local time.


### Binding redirect for NewtonSoft.Json

Relativity and the APIs consumed by Relativity use several different versions of the Newtonsoft.Json library, which can cause assembly version conflict at build time. This is the recommended assembly binding redirect to Newtonsoft.Json version 6.0.0.0 to add to the consuming application (`app.config`) or web (`web.config`) configuration file. It ensures that almost any version of `Newtonsoft.Json` will work with the TAPI:
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
      <startup>
      <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.6.2" />
      </startup>  
      <runtime>
            <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">             
                 <dependentAssembly>
                 <assemblyIdentity name="Newtonsoft.Json" publicKeyToken="30ad4fe6b2a6aeed" culture="neutral" />
                       <bindingRedirect oldVersion="0.0.0.0-9.0.0.0" newVersion="6.0.0.0" />
                 </dependentAssembly>
           </assemblyBinding>
      </runtime>
</configuration>
```
