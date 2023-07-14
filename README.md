> **NOTE**: Relativity.Transfer.Client version 7.4.10 (and below) **will stop functioning end of Q4 2023** (we will communicate the precise date 3 months before) due to migration of data transfer related products to cloud based solutions. 

>**Check out Relativity.Transfer.SDK ([NuGet](https://www.nuget.org/packages/Relativity.Transfer.SDK/)), a successor of this library. It works with all types of fileshares, it's faster and simpler in use!**
>
> Since it is going to be **a breaking change** you will need to **update your code** and integrate with the **Relativity.Transfer.SDK**, version 1.0.0+
>
> On December 5th, 2022 we have released the first version (1.0.3) including:
> * Upload a single file or a directory to the RelativityOne fileshare 
> * Track data transfer progress (including files that were transferred, skipped or failed, with their source and destination path)
> * Recover transfer from previous failure or interruption 
> * Real time information about succeeded, failed and skipped items, including exact path and the reason of error 
> * Cross platform compatibility - you can transfer data from Windows, Linux and macOS 
> * Secured transfer using HTTPS, only single TCP 443 is required 
>
> Q1 2023 you can expect additional functionalities like: 
> * Download a single file or a directory from the RelativityOne fileshare 
> * File retry policy 
> * File exclusion policy  
> * File override policy  
> * File custom attributes policy (used to set custom metadata on target file)

# Relativity Transfer API for .NET (deprecated since Dec 5th, 2022)
You can use the Transfer API (TAPI) to build application components that connect to Relativity and stream data from external sources into Relativity storage using different transfer protocols, for example, SMB or Aspera. You can also stream data from Relativity. The API enables optimized data transfer with extensible client architecture and event model using Relativity authentication and logging. For example, you can use the Transfer API to develop an application that loads case data into Relativity for subsequent processing. Unlike the Import API, TAPI doesn't create Relativity objects associated with the data, for example, documents and RDOs.

## Core TAPI features
The TAPI includes the following core features:

* Async/await design
* Thread-safe
* Transfers in a single request or by a request job
* Massive file counts and low resources
* Cancellation using CancellationToken
* Progress using a context object
* Diagnostics (for example, compatibility and connection checks)
* Relativity user authentication
* Built-in RCC package/transfer support
* Logging with the Relativity logging framework and [Serilog](https://serilog.net/)

We are  providing a sample solution to help you get started developing your own transfer applications.

## System requirements
* Bluestem release for RelativityOne or on-premises Relativity
* .NET 4.6.2
* Visual C++ 2010 x86 Runtime
* Intel 2Ghz (2-4 cores is recommended)
* 8GB RAM (32GB of RAM is recommended)

***Note:** The Visual C++ runtime is required for Open SSL and Aspera transfer client**

## Integrations
As of this writing, TAPI is now integrated within the following components and applications:

* Remote Desktop Client
* Import API
* Relativity One Staging Explorer

## Supported transfer clients
The transfer API uses [MEF (Managed Extensibility Framework)](https://docs.microsoft.com/en-us/dotnet/framework/mef/) design to search and construct clients. Relativity supports the following clients:

* Aspera
* File share

Aspera transfer mode requires access to configured Aspera service.
File share transfer mode requires access to locations involved in upload or download for example via vpn.

## Long path support
Long path support has been added in TAPI. Previous versions of TAPI had a Windows-defined maximum transfer path limit of 260 characters due to limitations with Microsoft.NET System.IO API calls. In addition to limiting the path length in the CLI, this limitation also had consequences for products that use TAPI (such as the RDC and ROSE), where attempting to transfer any paths over this 260 character limit would result in a transfer failure. This limitation existed regardless of the transfer client used.

The maximum path length now depends on the chosen transfer client.  These limits apply for both the source and full target path lengths.

| Transfer Client                | Maximum supported path length                       |
|--------------------------------|-----------------------------------------------------|
| File share                     | *N/A*                                               |
| Aspera                         | 470                                                 |

If a direct file share transfer is used, there is effectively no limit to the path length that can be performed. When using the Aspera transfer client, the maximum path length is now 470 due to limitations with the Aspera API. If a user specifies a source path or target path that is longer than the maximum supported path length, a fatal PathTooLongException will be thrown and the source file won't be transferred.

As part of these updates, a GlobalSetting variable has been added to adjust the behavior when a path that is too long for the chosen client to transfer is found during enumeration. This setting, called `SkipTooLongPaths`, is a boolean value. If `true`, any paths longer than the client supported maximum will be classified as an Error Path, and won't be transferred. However, enumeration and the transfer of all other valid paths will complete as part of the transfer job. If `false`, the enumeration will throw a fatal PathTooLongException upon encountering an invalid path length, and the transfer will fail. No files will be transferred in this situation.

## Sample solution
The `Sample.sln` solution is an out-of-the-box template for developing your own custom transfer applications and demonstrates Aspera and file share API usage.

Prerequisites for running the solution:

* Visual Studio 2015, 2017 or 2019
* A Relativity instance that you can connect to
* Valid Relativity credentials

Verify the solution builds successfully. Even though the application performs no real work yet, debug to ensure the application terminates with a zero exit code.

Ensure the following 5 settings found in app.config are updated with valid values:
* TransferMode
* RelativityUrl
* RelativityUserName
* RelativityPassword
* WorkspaceId

TransferMode valid values are "Aspera" and "Fileshare". Transfer will be executed in specified mode.

The next sections discuss Aspera and file share TAPI features.

* [Object model](#object-model)
* [Demo](#demo)

### Object model
The next several sections highlight the TAPI object model.

* [Cancellation](#cancellation)
* [InitializeGlobalSettings](#initializeglobalsettings)
* [CreateTransferLog](#createtransferlog)
* [CreateRelativityTransferHost](#createrelativitytransferhost)
* [CreateTransferClient](#createtransferclient)
* [Subscribe to transfer events](#subscribe-to-transfer-events)

#### Cancellation
The use of cancellation token is strongly recommended with potentially long-running transfer operations. The sample wraps a `CancellationTokenSource` object within a using block, assigns the `CancellationToken` object to a variable, and passes the variable to all asynchronous methods.

```csharp
using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
{
    CancellationToken token = cancellationTokenSource.Token;
    ...
}
```

#### InitializeGlobalSettings
The `InitializeGlobalSettings()` method is responsible for configuring the [GlobalSettings](#globalsettings).

#### CreateTransferLog
The `CreateTransferLog()` method uses a [Relativity Logging](https://platform.relativity.com/9.6/Content/Logging/Logging.htm) XML configuration file to create the [ITransferLog](#logging) instance used to log transfer details, warnings, and errors. It's entirely possible for API users to create an `ITransferLog` derived class object and use virtually any logging framework; however, the `RelativityTransferLog` class object is provided to simplify integration with Relativity Logging. The `LogConfig.xml` is designed to write all entries to a rolling log file within the user profile `%TEMP%` directory and a local [SEQ](https://getseq.net) log server.

#### CreateRelativityTransferHost
The `CreateRelativityTransferHost()` method defines a [RelativityConnectionInfo](#relativityconnectioninfo) object to specify the Relativity URL, credentials, and optional workspace artifact ID. The URL and credentials are supplied to all HTTP/REST endpoints and TAPI supports both basic authentication and OAuth2 bearer tokens. For more information about Relativity OAuth2 clients, see [Relativity Documentation Site]("https://help.relativity.com/RelativityOne/Content/Relativity/Authentication/OAuth2_clients.htm"). Once constructed, the `RelativityConnectionInfo` object is passed to the [RelativityTransferHost](#relativitytransferhost) constructor.

#### CreateTransferClient
If a workspace artifact is specified within the `RelativityConnectionInfo` object, the `CreateClientAsync()` method is designed to query the workspace, determine which transfer clients are supported (Aspera or file share), and choose the optimal client. *If* a client is specified within the `ClientConfiguration` object, the `CreateClient()` method explicitly instructs TAPI to construct a certain type of client. There may be circumstances where direct access to the file share is guaranteed and the 
Client will always be your best transfer option. For more information, see [Dynamic Transfer Client](#dynamic-transfer-client) and [ITransferClient](#itransferclient).

#### Subscribe to transfer events
Since transfer events are used for both upload and download operations, the demo wraps this within the `CreateTransferContext()` method to construct the [TransferContext](#transfer-events-and-statistics) object and write event details to the console. This object is used to decouple the event logic of the transfer - for example, progress and  statistics - from the host and the client.

### Demo
Real-world applications typically involve large or even massive datasets that not only require better transfer request management but provide real-time data rate, progress, and time remaining to their users.

For first time executions, Windows may popup a `Windows Defender` window like the one below. If this is presented, click the "Allow access" button.

![windowsdefender-firewall](https://user-images.githubusercontent.com/32276163/45306961-b2c22580-b4d2-11e8-8e9b-8ee90168d7f9.png)

For this demo, the approach is as follows:

* Create an Aspera or file share specific TAPI client
* Specify a target file share
* Create an upload transfer job request and job
* Search for the local dataset and add the local transfer paths to the job
* Await completion and display the results
* Create a download transfer job request and job
* Add the remote transfer paths to the job
* Change the data rate, await completion, and display the results

#### Create ClientConfiguration object
The `CreateClientConfiguration()` method is responsible for creating and configuring the [ClientConfiguration](#clientconfiguration) object. This object inherits all of the configurable properties found within [ClientConfiguration](#clientconfiguration), adds numerous Aspera specific transfer properties if Aspera client selected, and assigns `Aspera` or `Fileshare` to the the `Client` property. This value is later evaluated by the `CreateClientAsync()` method to construct the specified TAPI client.

#### Search for specific file share
Using a workspace to drive the selected file share is convenient but doesn't meet all workflow requirements. For example, consider a data migration application to move files from on-premise to RelativityOne. In this scenario, the target workspace may not even exist; however, the migration operator knows precisely which file share should be used. For scenarios like these, the [File Storage Search](#file-storage-search) API is provided.

***Note:**  You must be an admin to retrieve file shares from the instance. See [Targeting file shares](#targeting-file-shares) for more details.*

***Note:**  The `GetRelativityFileShare()` method supports retrieving file shares by artifact, UNC path, logical number, and name.*

Once the file share is retrieved by the `GetFileShareAsync()` method, the object is simply assigned to the `TargetFileShare` property found within the `ClientConfiguration` object.

#### Search local source paths
Data transfer workflows often involve large datasets stored on network servers or other enterprise storage devices. In many cases, the data transfer operator would like to transfer all of the files contained within a specified path. It's achieved with enumerators, created with `EnumerationBuilder` class.
Enumerators supports features like:
* reporting current statistics, 
* batching (dividing transfer on smaller chunks),
* filtering files and directories.

For large datasets (IE 1M+), it's recommended to serialize the results to disk and batching the results in smaller chunks. Since the test dataset is small, the enumeration option is used. Enumeration returns the `IEnumerable` of `TransferPath` objects and uses the lambda expression to report useful statistics.

#### Create job TransferRequest objects
When defining a `TransferRequest` object to support transfer jobs, `TransferPath` objects are *never* added to the request as this responsibility is handled by the transfer job. Among the available overloads, the `ForUploadJob()` and `ForDownloadJob()` methods accept a target path and `TransferContext` object.

#### Transfer jobs
The transfer client is used to construct a new transfer job where `TransferPath` objects can be added at any point in time. It's understood that files are *immediately* transferred in the order that they've been added to the job. The `ITransferJob` instance is wrapped in a using block so that all resources are properly disposed.

#### Change the data rate
One of the other advantages with using a job is that it provides the API caller an object to perform job-specific operations like increasing or decreasing the data rate. Because not all TAPI client support this feature, a convenient `IsDataRateChangeSupported` property is provided by the `ITransferJob` object.

#### Start or debug
Start or debug the project and ensure 5 files are successfully uploaded/downloaded and the application terminates with a zero exit code.

The following sections provide detailed reference for the Transfer API operations illustrated by the sample program above.

## Usage
The next sections cover TAPI usage including:

* [RelativityConnectionInfo](#relativityconnectioninfo)
* [RelativityTransferHost](#relativitytransferhost)
* [ClientConfiguration](#clientconfiguration)
* [AsperaClientConfiguration](#asperaclientconfiguration)
* [ITransferClient](#itransferclient)
* [Dynamic transfer client](#dynamic-transfer-client)
* [ITransferClientStrategy](#itransferclientstrategy)
* [Client support check](#client-support-check)
* [Connection check](#connection-check)
* [IRemotePathResolver](#iremotepathresolver)
* [IRetryStrategy](#iretrystrategy)
* [TransferPath](#transferpath)
* [ITransferRequest](#itransferrequest)
* [ITransferResult](#itransferresult)
* [TransferStatus](#transferstatus)
* [ITransferJob](#itransferjob)
* [Transfer via request](#transfer-via-request)
* [Transfer via job](#transfer-via-job)
* [Workspaces and the default file share](#workspaces-and-the-default-file-share)
* [Targeting file shares](#targeting-file-shares)
* [Local and remote enumeration](#local-and-remote-enumeration)
* [Change job data rate](#change-job-data-rate)
* [Transfer events and statistics](#transfer-events-and-statistics)
* [Transfer application performance monitoring and metrics](#transfer-application-performance-monitoring-and-metrics)
* [Error handling and ITransferIssue](#error-handling-and-itransferissue)
* [GlobalSettings](#globalsettings)
* [Logging](#logging)
* [Relativity version check](#relativity-version-check)
* [DateTime object values](#datetime-object-values)
* [Binding redirect for Json.NET](#binding-redirect-for-json.net)
* [Packaging and RCC package library](#packaging-and-rcc-package-library)

### RelativityConnectionInfo
The first thing you must do is construct a `RelativityConnectionInfo` object, which requires the following:

| Property        | Description                                                                                                                    |
| --------------- |--------------------------------------------------------------------------------------------------------------------------------|
| Host            | The Relativity URL.                                                                                                            |
| Credential      | The Relativity credential used to authenticate HTTP/REST API calls.                                                            |
| WorkspaceId     | The workspace artifact identifier used to auto-configure the request with file share, credential, and other transfer settings. |

***Note:**  The workspace artifact identifier can be set to `Workspace.AdminWorkspaceId` if the workspace is unknown or the transfer is manually configured. See [Admin Workspace](#admin-workspace) and [Targeting file shares](#targeting-file-shares) for more details.*

The following example uses basic username/password credentials.

```csharp
// This will eventually use the specified workspace to auto-configure the transfer.
const int WorkspaceId = 111111;
var connectionInfo = new RelativityConnectionInfo(
    new Uri("http://localhost/Relativity"),
    new BasicAuthenticationCredential("relativity.admin@relativity.com", "MyUbreakablePassword777!"),
    WorkspaceId);
```

When using an OAUTH2 client to authenticate, the bearer token is provided instead.

```csharp
const int WorkspaceId = 111111;
// This will eventually use the specified workspace to auto-configure the transfer.
var connectionInfo = new RelativityConnectionInfo(
    new Uri("http://localhost/Relativity"),
    new BearerTokenCredential(bearerToken),
    WorkspaceId);
```

When manually configuring the transfer, do **not** pass the workspace artifact. In this scenario, the [Admin Workspace](#admin-workspace) is implicitly specified.

```csharp
// This won't auto-configure the transfer.
var connectionInfo = new RelativityConnectionInfo(
    new Uri("http://localhost/Relativity"),
    new BasicAuthenticationCredential("relativity.admin@relativity.com", "MyUbreakablePassword777!"));
```

### RelativityTransferHost
Given the `RelativityConnectionInfo` object, the `RelativityTransferHost` object is then constructed. This object implements `IDisposable` to manage object life-cycles and should employ a using block. This object has several key responsibilities including:

* Defines *which* Relativity instance is used for all transfer requests
* Use `CreateClient` to [construct a specific ITransferClient](#itransferclient) object
* Use `CreateClientAsync` to [dynamically construct the best ITransferClient](#dynamic-transfer-client) object
* Use `GetWorkspaceAsync` to [retrieve a workspace object](#workspaces-and-the-default-file-share) that includes the default file share
* Use `CreateFileStorageSearch` to [retrieve all file share](#file-storage-search) objects and target a specific file share
* Use `VersionCheckAsync` to [perform a Relativity version check](#relativity-version-check)

```csharp
using (IRelativityTransferHost host = new RelativityTransferHost(connectionInfo))
{ }
```

***Note:** This object implements the `IDisposable` interface to ensure the life-cycle is managed properly.*

### ClientConfiguration
Before you can create a client, you have to provide a `ClientConfiguration` instance. If you know which client you would like to construct, choose a strongly-typed class object that derives from `ClientConfiguration`. The vast majority of settings contained within this class object are supported by all clients unless otherwise specified.

| Property                   | Description                                                                                                                                                                                                                                 | Default Value                      |
| ---------------------------| --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|------------------------------------|
| BadPathErrorsRetry         | Enable or disable whether to retry intermittent `TransferPathStatus.BadPathError` I/O errors or treat as a fatal error.           						                                                                               | false						        |
| BcpRootFolder              | The name of the folder, located at the root of the file shares, where all bulk-load files are stored.             						                                                                                                   | null  					            |
| Client                     | The transfer client unique identifier. This is automatically set when the transfer client is constructed via best-fit strategy.                                                                                                             | WellKnownTransferClient.Unassigned |
| ClientId                   | The well-known transfer client value. This is automatically set when the transfer client is constructed via best-fit strategy.                                                                                                              | Guid.Empty                         |
| CookieContainer            | The HTTP cookie container.                                                                                                                                                                                                                  | new instance                       |
| Credential                 | The optional credential used in place of the workspace file share credential. Only specify the `Credential` or `TargetFileShare` property but not both.                                                                                  | null                               |
| FileNotFoundErrorsDisabled | Enable or disable whether to treat missing files as warnings or errors.                                                                                                                                                                     | false                              |
| FileNotFoundErrorsRetry    | Enable or disable whether to retry missing file errors.                                                                                                                                                                                     | true                               |
| FileSystemChunkSize        | The size of each byte chunk transferred over file-system based transfer clients.                                                                                                                                                            | 16KB                               |
| FileTransferHint           | The hint provided to the transfer client that indicates what type of transfer workflow is being requested. This is generally used by transfer clients to tune and optimize the transfer.                                                    | FileTransferHint.Natives           |
| HttpTimeoutSeconds         | The timeout, in seconds, for REST service call.                                                                                                                                                                                  | 300 seconds                        |
| MaxHttpRetryAttempts       | The maximum number of retry attempts for REST service call.                                                                                                                                                                      | 5                                  |
| MaxJobParallelism          | The maximum number of threads used to transfer all paths contained within the transfer job queue.                                                                                                                                           | 1                                  |
| MaxJobRetryAttempts        | The maximum number of transfer job retry attempts.                                                                                                                                                                                          | 3                                  |
| MinDataRateMbps            | The minimum data rate in Mbps unit. This isn't supported by all clients but considered a hint to clients that support configurable data rates.                                                                                              | 0                                  |
| OverwriteFiles             | Enable or disable whether to overwrite files at the target path. The transfer job will fail when this option is disabled and target paths already exist.                                                                                    | true                               |
| PermissionErrorsRetry      | Enable or disable whether to retry transferring files that fail due to permission or file access errors.          								                                                                                           | false  						    |
| PreCalculateJobSize        | Enable or disable whether to pre-calculate the file/byte totals within the job to improve progress accuracy. This feature is deprecated and [Local and Remote Enumeration](#local-and-remote-enumeration) should be used instead.           | false                              |
| PreserveDates              | Enable or disable whether to preserve file created, modified, and access times.                                                                                                                                                             | true                               |
| SupportCheckPath           | The optional path used during auto-configuration to allow each potential client perform additional support checks. This is typically used when a client is supported but may not have access or proper configuration to the specified path. | null                               |
| TargetDataRateMbps         | The target data rate in Mbps unit. This isn't supported by all clients but considered a hint to clients that support configurable data rates.                                                                                               | 100 Mbps                           |
| TargetFileShare            | The optional target file share used in place of the workspace file share. Only specify the `Credential` or `TargetFileShare` property but not both.                                                                                      | null                                |
| TransferEmptyDirectories   | Enable or disable whether to transfer empty directories.                                                                                                                                                                                    | false                              |       
| TransferLogDirectory       | The directory where transfer clients can store more detailed transfer logs separate from standard logging.                                                                                                                                  | null                               |
| ValidateSourcePaths        | Enable or disable whether to validate source paths before adding to the transfer job queue. When enabled, this can be an expensive operation if the dataset contains a large number of files.                                               | true                               |


***Note:** API users are strongly encouraged to set the `TransferLogDirectory` because clients that support this feature typically write more detailed diagnostic information into their custom log files.*


### AsperaClientConfiguration
The Aspera transfer engine defines a large number of properties to customize the transfer request. As a result, the `AsperaClientConfiguration` class object is more extensive compared to any client and special care must be taken when changing or deviating from some of the default values.

| Property                      | Description                                                                                                                                                                                                                                                                                                                                                                               | Default Value              |
| ------------------------------| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------|
| AccountUserName               | The optional Aspera account username. This should only be set when overriding the auto-configured credential. This is retrieved from the workspace's default file share when the client is first initialized.           			                                                                                                                                                        | null                       |
| AccountPassword               | The optional Aspera account password. This should only be set when overriding the auto-configured credential. This is retrieved from the workspace's default file share when the client is first initialized.           			                                                                                                                                                        | null                       |
| DocRootLevels                 | The number of levels the Aspera doc root folder is relative to the file share resource server UNC path. This should never be changed unless the server configuration has deviated from the default.                          			                                                                                                                                                    | 1                          |
| DocRootLevels                 | The number of levels the Aspera doc root folder is relative to the file share resource server UNC path. This should never be changed unless the server configuration has deviated from the default.                          			                                                                                                                                                    | 1                          |
| HealthCheckLogMaxLine         | The maximum number of lines to retrieve from the Aspera transfer log when performing a diagnostic check.                                                                                                                                                                                                                                                                                  | 100                        |
| Host                          | The optional Aspera host name. This should only be set when overriding the auto-configured credential. This is retrieved from the workspace's default file share when the client is first initialized.                   			                                                                                                                                                        | null                       |
| CreateDirectories             | Enable or disable whether to automatically create directories when they don't already exist.                                                                                                                                  			                                                                                                                                                | true                       |
| EncryptionCipher              | The cipher used to encrypt all transferred data. Accepted values include: NONE|AES_128|AES_192|AES_256                                                                                                                                  			                                                                                                                                        | AES_256                    |
| FaspDebugEnabled              | Enable or disable whether to apply debug logging to the transfer logs.                                                                                                                                 			                                                                                                                                                                        | false                      |
| MetaThreadCount               | The number of threads the Aspera receiver uses to create directories or 0 byte files. It takes effect on both client and server, when acting as a receiver. The default of zero causes the Aspera receiver to use its internal default, which may vary by operating system. This is a performance-tuning parameter for an Aspera receiver.                                                | 0                          |
| NodeAccountUserName           | The optional Aspera Node API account user name. This should only be set when overriding the auto-configured credential. This is retrieved from the workspace's default file share when the client is first initialized.                                                                                                                                                                   | null                       |
| NodeAccountPassword           | The optional Aspera Node API account password. This should only be set when overriding the auto-configured credential. This is retrieved from the workspace's default file share when the client is first initialized.                                                                                                                                                                    | null                       |
| NodeHost                      | The optional Aspera Node API host name. This should only be set when overriding the auto-configured credential. This is retrieved from the workspace's default file share when the client is first initialized.                                                                                                                                                                           | null                       |
| OverwritePolicy               | The policy that determines whether to overwrite files at the target path. The transfer job will fail when this option is disabled and target paths already exist. Accepted values include: DIFFERENT|ALWAYS|DIFFERENT_AND_OLDER|NEVER|OLDER.                                                                                                                                              | ALWAYS                     |
| PartialFileSuffix             | The filename extension on the destination computer while the file is being transferred. Once the file has been completely transferred, this filename extension is removed. This must be specified when the OverwritePolicy is set.                                                                                                                                                        | .partial                   |
| Policy                        | The default transfer rate and bandwidth policy. Care must be taken when deviating from the default FAIR policy since it can cause significant transfer errors if an overly aggressive setting is used. Accepted values include: FAIR|FIXED|HIGH|LOW.                                                                                                                                      | FAIR                       |
| ReadThreadCount               | The number of threads the Aspera sender uses to read file contents from the source disk drive. It takes effect on both client and server when acting as a sender. The default of zero causes the Aspera sender to use its internal default, which may vary by operating system. This is a performance-tuning parameter for an Aspera sender.                                              | 0                          |
| ResumeCheck                   | The resume policy for partially transferred files. When specified, retry attempts can take longer if the dataset includes a large number of small files.                                                                                                                                                                                                                                  | OFF                        |
| SaveBeforeOverwriteEnabled    | Enable or disable whether to modify a filename that would overwrite an existing file by renaming to filename.yyyy.mm.dd.hh.mm.ss.index.ext (where index is set to 1 at the beginning of each new second and incremented for each file saved in this manner during the same second) in the same directory before writing the new file. File attributes are maintained in the renamed file. | false                      |
| ScanThreadCount               | The number of threads the Aspera sender uses to scan directory contents. It takes effect on both client and server, when acting as a sender. The default of zero causes the Aspera sender to use its internal default. This is a performance-tuning parameter for an Aspera sender.                                                                                                       | 0                          |
| TcpPort                       | The TCP port used for transfer initialization.                                                                                                                                                                                                                                                                                                                                            | 33001                      |
| TestConnectionDestinationPath | The remote destination path where all test connection zero-byte files are transferred.                                                                                                                                                                                                                                                                                                    | /FTA/TestConnectionResults |
| UdpPortStartRange             | The UDP start port range used for transferring data.                                                                                                                                                                                                                                                                                                                                      | 33001                      |
| UdpPortEndRange               | The UDP end port range used for transferring data.                                                                                                                                                                                                                                                                                                                                        | 33050                      |
| WriteThreadCount              | The number of threads the Aspera receiver uses to write the file contents to the destination disk drive. It takes effect on both client and server, when acting as a receiver. The default of zero causes the Aspera receiver to use its internal default, which may vary by operating system. This is a performance-tuning parameter for an Aspera receiver.                             | 0                          |

### ITransferClient
The API caller uses the transfer host to construct the appropriate transfer client. This object implements `IDisposable` to manage client specific object life-cycles. **MEF** is used to dynamically construct the appropriate instance.

```csharp
// I need an Aspera client.
using (ITransferClient client = host.CreateClient(new AsperaClientConfiguration()))
{ }

// I need a file share client.
using (ITransferClient client = host.CreateClient(new FileShareClientConfiguration()))
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

* File share
* Aspera

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
The `ITransferRequest` object exposes optional `SourcePathResolver` and `TargetPathResolver` properties to take a given path and adapt it. The signature for this interface is as follows:

```csharp
interface IRemotePathResolver
{
    string ResolvePath(string path);
}
```

The primary use-case for path resolvers is adding backwards compatibility to a client. For example, the Aspera client employs resolvers to adapt UNC paths to native Aspera UNIX-style paths.

***Note:** The library provides **AsperaUncPathResolver** for API users that wish to use Aspera clients when using UNC paths exclusively. This is **automatically** used by Aspera jobs as long as `ITransferRequest.RemotePathsInUncFormat` is true.

### IRetryStrategy
The `ITransferRequest` object exposes an optional `RetryStrategy` property to define a method that dictates how much time is spent in between retry attempts. The signature for this interface is as follows:

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

### ITransferRequest
There are several options to submit transfers and all revolve around the `ITransferRequest` object.

| Property               | Description |
| ---------------------- |------------------------------------------------------------------------------------------------------ |
| Application            | The **optional** application name for this request. When specified, this value is attached to the APM metrics and other reporting features for added insight. |
| BatchNumber            | The current batch number. This is automatically assigned when using batches to transfer. |
| ClientRequestId        | The **optional** client specified transfer request unique identifier for the entire request. If not specified, a value is automatically assigned. |
| Context                | The **optional** transfer context to configure progress events and logging. |
| Direction              | The **optional** transfer direction (Upload or Download). This is a global setting which, if specified, automatically updates all `TransferPath` objects **if not already assigned**. |
| JobId                  | The **optional** unique identifier for the current submission or job. If not specified, a value is automatically assigned. |
| Name                   | The **optional** name associated with this request. For Aspera transfers, this value is attached to all reporting data and useful for identification and search purposes. |
| Paths                  | The transfer path objects. This is ignored when using a transfer job. |
| RemotePathsInUncFormat | The **optional** value indicating whether remote paths are in UNC format. This is set to `true` by default. |
| RetryStrategy          | The **optional** IRetryStrategy instance to define the amount of time to wait in between each retry attempt. By default, an exponential backoff strategy is assigned. |
| SourcePathResolver     | The **optional** resolver used to adapt source paths from one format to another. This is set to `NullPathResolver` by default. |
| SubmitApmMetrics       | Enable or disable whether to submit APM metrics to Relativity once the transfer job completes. This is set to `true` by default. |
| Tag                    | The **optional** object allows storing any custom object on the transfer request. |
| TargetPath             | The transfer target path. This is a global setting which, if specified, automatically updates all `TransferPath` objects **if not already assigned**. |
| TargetPathResolver     | The **optional** resolver used to adapt target paths from one format to another. This is set to `NullPathResolver` by default. |
| TotalBatchCount        | The total number of batches. This is automatically assigned when using batches to transfer. |

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

### FileTransferHint
This enumeration provides hints to better configure or optimize the transfer request.

| Name                | Description |
| ------------------- | ------------------------------------- |
| Natives             | The request involves transferring native files. |
| BulkLoad            | The request involves transferring bulk load files. |

### TransferStatus
This enumeration provides the overall transfer status.

| Name                | Description |
| ------------------- | ------------------------------------- |
| NotStarted          | The transfer hasn't started. |
| Failed              | The transfer contains one or more paths that failed to transfer. These errors are non-fatal and can be retried. |
| Fatal               | The transfer stopped due to a fatal error. |
| Successful          | The transfer is successful. |
| Canceled            | The transfer is canceled. |

### ITransferJob
Once a client is constructed, transfer jobs are created in one of two ways (see below). Once constructed, the job adheres to a common structure regardless of client. The job has several key responsibilities including:

* Provide a life-cycle for the entire transfer.
* Add paths to a job queue.
* Create local or remote path enumerators to search for paths or partition large datasets into smaller chunks.
* Change the data rate at runtime.
* Wait for all transfers to complete.
* Status and current statistics.
* Retry count.
* Retrieving a list of all processed job paths.

***Note:** This object implements the `IDisposable` interface to ensure the life-cycle is managed properly. If any operations are performed on the job after the object has been disposed, an `ObjectDisposedException` will be thrown.*

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

This is an ideal approach to take if the number of files is **small** and the required functionality is minimal.

### Transfer via job
If the **list of source paths is unknown**, you want to avoid calling `TransferAsync` one file at a time. High-speed clients like Aspera require a significant amount of overhead to setup the transfer request and performance would suffer significantly. To address this scenario, the `ITransferJob` object can be constructed via the client instance. The general idea is to construct a job, continually add transfer paths to the queue as they become known, and then await completion. Beyond large transfers, the `ITransferJob` object provides several other functional advantages such as changing the data rate at runtime and a predictable object life-cycle.

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

### Workspaces and the default file share
The vast majority of workflows within Relativity center on the workspace. Additionally, the workspace configuration is responsible for defining several resource servers including the default file repository. API users can retrieve a `Workspace` object through `IRelativityTransferHost` to assist configuring the transfer request.

```csharp
using (IRelativityTransferHost host = new RelativityTransferHost(this.connectionInfo))
{
    // Use the workspace specified within RelativityConnectionInfo.
    Workspace workspace1 = await host.GetWorkspaceAsync(token).ConfigureAwait(false);
    RelativityFileShare fileShare1 = workspace1.DefaultFileShare;

    // Or you can specify the workspace instead.
    const int WorkspaceId = 1234567;
    Workspace workspace2 = await host.GetWorkspaceAsync(WorkspaceId, token).ConfigureAwait(false);
    RelativityFileShare fileShare2 = workspace2.DefaultFileShare;
}
```

The `Workspace` object provides the following properties:

| Property                     | Description |
| ---------------------------- |---------------------------------------------------------------------------------------------------------------------------------- |
| ArtifactId                   | The workspace artifact identifier.                                                                                                |
| DefaultFileShare             | The default file share associated with this workspace. This is always null for the `AdminWorkspace` object.                       |
| DefaultFileShareUncPath      | The full UNC path for the default file share associated with this workspace. This is always null for the `AdminWorkspace` object. |
| DistributedSqlResourceServer | The distributed SQL resource server associated with this workspace. This is always null for the `AdminWorkspace` object.          |
| DownloadHandlerUrl           | The download handler URL. This is always null for the `AdminWorkspace` object.                                                    |
| FileShareResourceServers     | The list of all file share resource servers associated with this workspace. This is always empty for the `AdminWorkspace` object. |
| Guids                        | The list of all artifact unique identifiers.                                                                                      |
| Name                         | The workspace name.                                                                                                               |
| PrimarySqlResourceServer     | The primary SQL resource server associated with this workspace. This is always null for the `AdminWorkspace` object.              |
| SqlResourceServers           | The list of all SQL resource servers associated with this workspace. This is always empty for the `AdminWorkspace` object.        |

#### Admin workspace
The `Workspace` object also defines the `AdminWorkspaceId` constant and the `AdminWorkspace` static property for situations where a workspace artifact cannot be specified or the API user would like to [target a file share](#targeting-file-shares) instead of using the default file share.

```csharp
// Obtain the admin workspace artifact.
const int WorkspaceId = Workspace.AdminWorkspaceId;

// Or obtain the admin workspace object instead.
Workspace workspace = Workspace.AdminWorkspace;
```

### Targeting file shares
When the `IRelativityTransferHost` object is constructed, the API user is required to supply a `RelativityConnectionInfo` instance. This object includes the workspace artifact identifier and is used by transfer clients to setup the transfer using the default file repository. This is  known as auto-configuration and is ideal for API users because it simplifies the transfer setup process.

Although many Relativity applications are geared around the workspace, there are situations where the workspace may not be known, doesn't exist, or the application simply wants to specify a target file share. For more advanced transfer scenarios, the `IFileStorageSearch` API is used to retrieve file shares and the `ClientConfiguration` object is assigned the target file share.

#### Resource pools and security
The workspace represents both a functional and security boundary required by many Relativity API's. The resource pool is the set of servers and file repositories associated with the workspace. Bypassing workspaces and querying these objects directly represents a security risk and, as such, requires `View Admin Repository` Admin Operations rights. In short, the API user must have **Admin Rights** to search for *all* file shares or specify *non-workspace* search criteria.

#### RelativityFileShare
This object represents a standard file share resource server and includes several RelativityOne cloud-specific properties.

| Property           | Description                                                                                                                                                                       |
| ------------------ |---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| ArtifactId         | The file share resource artifact identifier.     |
| AsperaCredentials  | The Aspera credentials for both storage authentication and REST API file management support. This is only assigned when `CloudInstance` is `true`. |
| CloudInstance      | The value indicating whether the Relativity instance is running within the RelativityOne cloud environment. |
| Credential         | The credential used for storage authentication. This is only assigned when `CloudInstance` is `true`. |
| DocRoot            | The path within the configured file share resource where all transfers are rooted. This is only assigned when `CloudInstance` is `true`. |
| Error              | The error message assigned when attempting to setup or configure the storage. |
| Name               | The file share name. |
| Number             | The file storage logical number. This is only assigned when `CloudInstance` is `true`. |
| ResourceServerType | The resource server type. |
| TenantId           | The tenant identifier associated with this file storage. This is only assigned when `CloudInstance` is `true`. |
| Url                | The URL that represents the file storage location or path. |

#### File storage search
The `IFileStorageSearch` API is constructed from the `IRelativityTransferHost` object and used to search for and identify file shares. Similar to other TAPI methods, a context object provides additional configuration details. Since none of the context properties are assigned, the example below searches for all file shares.

```csharp
// In this example, the workspace is NOT supplied to RelativityConnectionInfo.
RelativityConnectionInfo connectionInfo = new RelativityConnectionInfo(host, credential);
using (IRelativityTransferHost host = new RelativityTransferHost(connectionInfo))
{
    IFileStorageSearch fileStorageSearch = host.CreateFileStorageSearch();
    FileStorageSearchContext context = new FileStorageSearchContext();
    FileStorageSearchResults results = await fileStorageSearch.SearchAsync(context, token).ConfigureAwait(false);
    foreach (RelativityFileShare fileShare in results.FileShares)
    {
        Console.WriteLine("File share name: " + fileShare.Name);
        Console.WriteLine("File share path: " + fileShare.Url);
    }
}
```

#### Search options
The search criteria is exposed through the `FileStorageSearchContext` object. **Full system admin rights are required for all non-workspace search criteria** and includes the following search options:

| Property                           | Description                                                                                                                                                                                                      |
| ---------------------------------- |----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| ResourcePoolCondition              | The search is filtered by a resource pool condition.                                                                                                                                                             |
| ResourcePoolName                   | The search is filtered by a resource pool name.                                                                                                                                                                  |
| ResourceServerCondition            | The search is filtered by a resource server condition.                                                                                                                                                           |
| ResourceServerId                   | The search is filtered by a resource server artifact identifier.                                                                                                                                                 |
| WorkspaceCondition                 | The search is filtered by a workspace condition.                                                                                                                                                                 |
| WorkspaceId                        | The search is filtered by a workspace artifact identifier. All file shares can be retrieved by setting this value to [Workspace.AdminWorkspaceId](#admin-workspace) **but requires full system admin rights.**               |

***Note:** All condition search options support standard Relativity query operators.*

The following example uses the context to search for all files shares within the specified workspace:

```csharp
// This does NOT require admin rights.
FileStorageSearchContext context = new FileStorageSearchContext { WorkspaceId = 1123815 };
FileStorageSearchResults results = await fileStorageSearch.SearchAsync(context, token).ConfigureAwait(false);
```

The following example uses the context to search for all files shares within any resource pool that matches the specified condition.

```csharp
// This DOES require admin rights.
FileStorageSearchContext context = new FileStorageSearchContext { ResourcePoolCondition = "'Name' == 'default'" };
FileStorageSearchResults results = await fileStorageSearch.SearchAsync(context, token).ConfigureAwait(false);
```

The following example uses the context to search for the the file share that matches the specified resource server.

```csharp
// This DOES require admin rights.
FileStorageSearchContext context = new FileStorageSearchContext { ResourceServerId = 1049366 };
FileStorageSearchResults results = await fileStorageSearch.SearchAsync(context, token).ConfigureAwait(false);
```

The following example uses the context to search for all files shares within the instance:

```csharp
// This DOES require admin rights.
FileStorageSearchContext context = new FileStorageSearchContext { WorkspaceId = Workspace.AdminWorkspaceId };
FileStorageSearchResults results = await fileStorageSearch.SearchAsync(context, token).ConfigureAwait(false);
```

#### Search results
The `SearchAsync` returns a `FileStorageSearchResults` object to provide all search results.

| Property                           | Description                                                                                                                                                                                                      |
| ---------------------------------- |----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| BulkLoadFileShares                 | The read-only collection of the valid bulk load related file shares.                                        |
| ConnectionInfo                     | The Relativity connection information used to obtain the results.                                           |
| CloudInstance                      | The value indicating whether the Relativity instance is running within the RelativityOne cloud environment. |
| FileShares                         | The read-only collection of the valid file shares.                                                          |


This object provides helper methods to search for file shares by artifact or UNC path. The following example demonstrates retrieving the file share by the specified artifact identifier:

```csharp
FileStorageSearchResults results = await fileStorageSearch.SearchAsync(context, token).ConfigureAwait(false);
const int FileShareArtifactID = 1049366;
RelativityFileShare fileShare = results.GetRelativityFileShare(FileShareArtifactID);
```

#### Specify a file share
Given a file share object, the target file share is assigned within the `ClientConfiguration` object.

```csharp
RelativityFileShare fileShare = results.FileShares.FirstOrDefault();
ClientConfiguration configuration = new ClientConfiguration();
configuration.TargetFileShare = fileShare;
```

***Note:** When specifying a target file share and using **remote** UNC paths, it's imperative for the UNC paths to share the same base path as the file share. This can be problematic for transfer clients like Aspera that doesn't support native UNC paths.*



### Local and remote enumeration
So far, the examples have included a small number of transfer paths. What if you want to create transfer paths from one or more search paths? More importantly, what about datasets consisting of hundreds of thousands or even millions of paths? Due to the RAM and CPU required to manage such large datasets, a different approach is required.

TAPI provides an enumeration feature, which delivers the following capabilities:
* Time-efficient enumeration of local paths,
* Filtering paths,
* Grouping paths into batches and serializing them,
* Current statistics of enumeration's progress.

Local enumeration is provided out of the box whereas remote enumeration should be provided by a customer of a given API.

#### Define enumeration
The enumeration API is built upon fluent API pattern and can be easily customized. The `EnumerationBuilder` class serves the method to customize the enumeration for your needs:

The below samples depict the usage of EnumerationBuilder class, which exposes a fluent interface in order to build enumeration object.

#### Upload local files
The method `ForUpload()` will use the built-in, parallel enumerator to retrieve all paths for upload. It can be CPU-intensive, but it's the fastest method for it:
```csharp
/// <summary>
/// Creates enumeration builder for local enumeration
/// </summary>
/// <param name="logger">Logger that will be used to log enumeration execution</param>
/// <param name="correlationId">CorrelationId used to associate events happening throughout the operation</param>
public static EnumerationBuilder ForUpload(ITransferLog logger, Guid correlationId)
 
usage:
var enumeration = EnumerationBuilder
                    .ForUpload(logger, Guid.NewGuid())
                    .StartFrom(filesOrDirectories)
                    .Create();
```

You can provide your own enumerator too - in case of specific needs (e.g. you already have all paths to transfer and you don't want to search through catalogs):
```csharp
/// <summary>
/// Creates enumeration builder for local enumeration using custom enumerator
/// </summary>
/// <param name="enumerator">Custom enumerator</param>
/// <param name="logger">Logger that will be used to log enumeration execution</param>
/// <param name="correlationId">CorrelationId used to associate events happening throughout the operation</param>
public static IEnumerationNecessaryActionsBuilder ForUpload(IEnumeratorProvider enumerator, ITransferLog logger, Guid correlationId)

usage:
IEnumerationProvider customEnumerator = /* ... */;
var enumeration = EnumerationBuilder
                    .ForUpload(customEnumerator, logger, Guid.NewGuid())
                    .StartFrom(filesOrDirectories)
                    .Create();
```

#### Download files from remote server
In case of download, you need to provide a custom enumerator of remote paths to the method `ForDownload()`:
```csharp
/// <summary>
/// Creates enumeration builder for remote enumeration
/// </summary>
/// <param name="enumerator">Custom enumerator</param>
/// <param name="logger">Logger that will be used to log enumeration execution</param>
/// <param name="correlationId">CorrelationId used to associate events happening throughout the operation</param>
public static EnumerationBuilder ForDownload(IEnumeratorProvider enumerator, ITransferLog logger, Guid correlationId)
 
usage:
var enumeration = EnumerationBuilder
                    .ForDownload(remoteEnumerator, logger, Guid.NewGuid())
                    .StartFrom(filesOrDirectories)
                    .Create();
```

#### Set the entry point
You might notice the method `StartFrom(IEnumerable<INode> sourceNodes);` - it takes a list of files and folders to be iterated. Content of the folders will be iterated as well.

#### Required and additional configuration
The bare minimum to define an enumerator is to call methods:
1. `ForUpload()` or `ForDownload()`,
2. `StartFrom()`.
3. `Create()`

Below you'll find how to specify additional capabilities.

#### Filtering
Use the method `WithFilters()` to enrich enumeration object with filtering functionality. Provide collection of filters to be applied against each path and a callback function which will be executed every time a given path meets at least one filter.

```csharp
/// <summary>
/// Enriches enumeration process with filtering
/// </summary>
/// <param name="filters">Enumerable of custom filters</param>
/// <param name="skippedItemHandler">Callback handler executed every time when enumerated path meets at least one filter</param>
public EnumerationBuilder WithFilters(IEnumerable<INodeFilter> filters, IEnumerationHandler<EnumerationIssue> skippedItemHandler)
 
usage:
var filterHandler = new EnumerationHandler<EnumerationIssue>(item=>
                        {
                            Console.WriteLine($"{item.Path} {item.ErrorMessage}");
                        });
var enumeration = EnumerationBuilder
                    .ForDownload(remoteEnumerator, logger, Guid.NewGuid())
                    .StartFrom(filesOrDirectories)
                    .WithFilters(filters, filterHandler)
                    .Create();
```

TAPI provides the following implementations of `INodeFilter` - you can define your own.
* `AspxExtensionFilter` - filters out all files with `.aspx` extension (to prevent errors of Aspera transfers),
* `NoReadAccessFileNodeFilter` - checks user's permission against a file (can slow down enumeration significantly),
* `PathLengthFilter` - finds paths longer than Aspera and file share support,
* `R1PathSizeFilter` - finds paths longer than RelativityOne supports.

#### Statistics
You can define your handler to react on enumeration updates:

```csharp
/// <summary>
/// Enriches enumeration process with statistics reporting
/// </summary>
/// <param name="statisticsHandler">Callback handler executed on every enumerated path to report enumeration overall statistic</param>
public EnumerationBuilder WithStatistics(IEnumerationHandler<EnumerationStatistic> statisticsHandler)

usage:
var statisticsHandler= new EnumerationHandler<EnumerationStatistic>(stats =>
            {
                Console.WriteLine($"Total bytes: {stats.TotalBytes} Total files: {stats.TotalFiles} Total Empty directories: {stats.TotalEmptyDirectories}");
            });
 
var enumeration = EnumerationBuilder
                    .ForUpload(logger, Guid.NewGuid())
                    .StartFrom(filesOrDirectories)
                    .WithStatistics(progressHandler)
                    .Create();
```

#### Batching
Grouping paths in batches is recommended for transfers with more than 1M files. Batches are stored on disk and you have to specify a handler to intercept all stored batches.

```csharp
/// <summary>
/// Enriches enumeration process with dividing enumerated paths into chunks and converting them into batches
/// </summary>
/// <param name="destinationNode">The destination node.</param>
/// <param name="batchSerializationDirectory">A place to serialize series of paths that belong to batches</param>
/// <param name="batchCreatedHandler">Callback handler executed on every batch created</param>
/// <returns>This builder</returns>
public EnumerationBuilder WithBatching(INode destinationNode, IDirectory batchSerializationDirectory, IEnumerationHandler<SerializedBatch> batchCreatedHandler)
 
usage:
var batchCreatedHandler = new EnumerationHandler<SerializedBatch>(batch =>
    {
        Console.WriteLine($"{batch.BatchNumber} {batch.File} {batch.TotalFileCount}");
    });
 
var enumeration = EnumerationBuilder
                    .ForUpload(logger, Guid.NewGuid())
                    .StartFrom(filesOrDirectories)
                    .WithBatching(targetDirectoryOrDrive, directoryToStoreBatches, batchCreatedHandler)
                    .Create();
```

You can define batching parameters (i.e. max number of files and bytes in a single batch) within `GlobalSettings` class:
1. `GlobalSettings.MaxFilesPerBatch`
2. `GlobalSettings.MaxBytesPerBatch`

#### EnumerationBuilder - full interface
```csharp
public interface EnumerationBuilder : IEnumerationNecessaryActionsBuilder, IEnumerationFinalActionsBuilder
{
    static IEnumerationNecessaryActionsBuilder ForUpload(ITransferLog logger, Guid correlationId);

    static IEnumerationNecessaryActionsBuilder ForUpload(IEnumeratorProvider enumerator, ITransferLog logger, Guid correlationId);

    static IEnumerationNecessaryActionsBuilder ForDownload(IEnumeratorProvider enumerator, ITransferLog logger, Guid correlationId);

	IEnumerationFinalActionsBuilder StartFrom(IEnumerable<INode> sourceNodes);

    IEnumerationFinalActionsBuilder WithFilters(IEnumerable<INodeFilter> filters, IEnumerationHandler<EnumerationIssue> skippedItemHandler);

    IEnumerationFinalActionsBuilder WithStatistics(IEnumerationHandler<EnumerationStatistic> statisticsHandler);

    IEnumerationFinalActionsBuilder WithBatching(INode destinationNode, IDirectory batchSerializationDirectory, IEnumerationHandler<SerializedBatch> batchCreatedHandler);

    IEnumerationOrchestrator Create();
}
```


### Change job data rate (Aspera-Only)
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

| Property                     | Description |
| ---------------------------- |----------------------------------------------------------------------------------- |
| EndTime                      | The **local** end time of the transfer operation. |
| LargeFileProgress            | Occurs when a single large file progress has changed. |
| LargeFileProgressEnabled     | The value indicating whether `LargeFileProgress` events are raised. This value is disabled by default. |
| LargeFileProgressRateSeconds | The rate, in seconds, that large file progress events are raised. |
| StartTime                    | The **local** start time of the transfer operation. |
| StatisticsEnabled            | The value indicating whether `TransferStatistics` events are raised. This value is enabled by default. |
| StatisticsRateSeconds        | The rate, in seconds, that statistics events are raised. This value is 2 seconds by default. |
| TransferPathProgress         | Occurs when the transfer path progress changed. |
| TransferPathIssue            | Occurs when an issue occurs transferring a path. |
| TransferJobRetry             | Occurs when a transfer job is retried. |
| TransferRequest              | Occurs when the overall transfer request is started or ended. |
| TransferStatistics           | Occurs to provide overall progress, transfer rate, and total byte/file count info. |

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

### Transfer application performance monitoring and metrics
The section above describes several statistics that applications using TAPI can use for common use-cases like driving the application front-end. However, any application that uses TAPI is equally interested in not only accessing statistics but other key transfer details to monitor the overall application health.

TAPI-enabled applications are able to take advantage of the APM framework by enabling the following opt-in transfer request setting:

```csharp
ITransferRequest request;

// Application metrics can be identified using this property.
request.Application = "my application";

// Enable submitting APM metrics when the request has completed.
request.SubmitApmMetrics = true;
```

The following table outlines all TAPI APM metric fields:

| CustomData Metric Field Name         | Description |
| ------------------------------------ |----------------------------------------------------------------------------------- |
| Application                          | The application name specified in the `ITransferRequest` or `GlobalSettings` object. |
| AverageTransferRateMbps              | The final average transfer rate in Mbps units. |
| BatchNumber                          | The final batch number. This is only applicable when transferring via batches. |
| Client                               | The transfer client name. |
| ClientRequestId                      | The client request unique identifier used to setup the transfer request. |
| ConfigurationBadPathErrorsRetry      | The configuration setting that enables or disables whether to retry bad path errors.  |
| ConfigurationFileNotFoundErrorsRetry | The configuration setting that enables or disables whether to retry file not found errors. |
| ConfigurationFileTransferHint        | The configuration setting that specifies the file transfer hint. |
| ConfigurationMinDataRateMbps         | The configuration setting that specifies the minimum data rate in Mbps units. |
| ConfigurationMaxJobParallelism       | The configuration setting that specifies the maximum degree of job parallelism. |
| ConfigurationMaxJobRetryAttempts     | The configuration setting that specifies the maximum number of job retry attempts. |
| ConfigurationOverwriteFiles          | The configuration setting that enables or disables whether to overwrite existing files. |
| ConfigurationPermissionErrorsRetry   | The configuration setting that enables or disables whether to retry permission errors. |
| ConfigurationPreserveDates           | The configuration setting that enables or disables whether to preserve all file timestamps. |
| ConfigurationTargetDataRateMbps      | The configuration setting that specifies the target data rate in Mbps units. |
| Elapsed                              | The total elapsed time for the transfer request. |
| JobId                                | The job unique identifier. |
| JobErrorCode                         | The client-specific job error code. |
| JobErrorMessage                      | The job error message. |
| JobMinDataRateMbps                   | The job minimum data rate in Mbps units. This value reflects job data rate changes made when calling the `ChangeDataRateAsync` method. |
| JobTargetDataRateMbps                | The job target data rate in Mbps units. This value reflects job data rate changes made when calling the `ChangeDataRateAsync` method. |
| RetryAttempts                        | The total number of retry attempts. |
| Status                               | The overall transfer status. |
| TargetPath                           | The target path specified within the `ITransferRequest` object. |
| TotalBadPathErrors                   | The total number of bad path errors.  |
| TotalBatchCount                      | The total number of batches. This is only applicable when transferring via batches. |
| TotalFilePermissionErrors            | The total number of file permission errors. |
| TotalFilesNotFound                   | The total number of files not found. |
| TotalRequestBytes                    | The total number of request bytes. |
| TotalRequestFiles                    | The total number of request files. |
| TotalTransferredBytes                | The total number of transferred bytes. |
| TotalTransferredFiles                | The total number of transferred files. |
| WeightedTransferRateMbps             | The weighted transfer rate in Mbps units. |
| WorkspaceGuid                        | The workspace artifact unique identifier. |

***Notes:*** 

* *New Relic APM support is provided for all Relativity One environments.*

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

#### Error handling and retry behavior
If TAPI determines that a transfer error can be retried, it will attempt to retry the transfer for the configured number of attempts before throwing a `TransferException`. The following transfer errors can be configured to be retried:

| Setting Name          | Description |
| --------------------- | -------------------------------------------------------------------------------------------------------------- |
| BadPathErrorsRetry    | When TAPI encounters a bad path error from Aspera only, it will use this setting to determine whether it should retry. TAPI has checks in place to prevent paths from being passed in that are invalid or otherwise not able to be transferred, so this error has only been observed during times of high load when Aspera may not be throwing the correct error code. |
| PermissionErrorsRetry | When TAPI encounters a permission error when transferring using Aspera, Web or file share, it will use this setting to determine whether it should retry. Similar to bad path errors, this will typically be thrown during times of high load, when Aspera is not necessarily throwing the correct error code. |

The `ITransferStatistics` object has properties that indicate how many times the above errors have been encountered and retried. This object has a count of TotalBadPathErrors and TotalFilePermissionsErrors, which will be incremented every time these errors are encountered.

### GlobalSettings
A number of common but optional settings are exposed by the `GlobalSettings` singleton.

| Property                                   | Description                                                                                                                                          | Default Value                                      |
| -------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------|
| ApmFireAndForgetEnabled                    | Enables or disables whether to submit APM metrics via fire-and-forget or wait for the response.                                                      | true                                               |
| ApplicationName                            | The name of the application. This value is prefixed within all log entries.                                                                          | TAPI                                               |
| CacheExpiration                            | The timespan that defines when cached objects will expire. When set to TimeSpan.Zero, caching is effectively disabled.                               | 3600                                               |
| CloudFileShareRegexPatterns                | The list of regular expressions used to match friendly or FQDN UNC paths to cloud-based file shares using file share numbers and tenant identifiers. | \\files(\d?).+(T\d{3}\w?), \\bcp(\d?).+(T\d{3}\w?) |
| CommandLineModeEnabled                     | Enables or disables whether the runtime behavior is altered for command-line usage.                                                                  | false                                              |
| FatalHttpStatusCodes                       | Specifies the list of fatal HTTP status codes.                                                                                                       | 400, 401, 403                                      | 
| FatalHttpStatusCodeDetailedMessage         | The list of detailed fatal messages associated with the fatal HTTP status codes.                                                                     | DefaultFatalHttpStatusCodeDetailedMessage          |
| FatalWebExceptionStatusCodes               | Specifies the list of fatal web exception status values.                                                                                             | WebExceptionStatus.TrustFailure                    |
| FatalWebExceptionStatusCodeDetailedMessage | The list of detailed fatal messages associated with the fatal web exception status codes.                                                            | DefaultFatalWebExceptionStatusCodeDetailedMessage  |
| LogPackageSourceFiles                      | Specifies whether to log all source files added to the package. If true, the overhead can degrade package performance.                               | false                                              |
| MaxAllowedTargetDataRateMbps               | The maximum target data rate, in Mbps units, allowed by the transfer API.                                                                            | 600                                                |
| MaxBytesPerBatch                           | The maximum number of bytes per batch. This is only applicable when transferring via serialized batches.                                             | 100GB                                              |
| MaxFilesPerBatch                           | The maximum number of files per batch. This is only applicable when transferring via serialized batches.                                             | 50,000                                             |
| MemoryProtectionScope                      | The memory protection scope applied to all data protection API (DPAPI) usage.                                                                        | MemoryProtectionScope.SameProcess                  |
| NodePageSize                               | The default page size when making Aspera Node REST API calls.                                                                                        | 100                                                |
| PluginDirectory                            | The directory where all plugins are located.                                                                                                         | Working directory                                  |
| PluginFileNameFilter                       | The file name filter to limit which files are searched for plugins.                                                                                  | *.dll                                              |
| PluginFileNameMatch                        | The file name match expression to limit which files are searched for plugins.                                                                        | Relativity.Transfer                                |
| PluginSearchOption                         | The file search option used when searching for plugins.                                                                                              | SearchOption.TopDirectoryOnly                      |
| PrecalcCheckIntervalSeconds                | The number of seconds the pre-calculation values are checked to determine whether the value has changed.                                             | 1.0                                                |
| SkipTooLongPaths                           | Enable or disable skipping long paths found during search path enumeration.                                                                          | false                                              |
| StatisticsLogEnabled                       | Enables or disables whether to periodically log transfer statistics.                                                                                 | false                                              |
| StatisticsLogIntervalSeconds               | The interval, in seconds, that transfer statistics are logged.                                                                                       | 2.0                                                |
| StatisticsMaxSamples                       | The maximum number of statistics transfer rate samples to add additional weight to the remaining time calculation.                                   | 8                                                  |
| TempDirectory                              | The directory used for temp storage.                                                                                                                 | Current user profile temp path (IE %TEMP%)         |
| ValidateResolvedPaths                      | Enable or disable whether to throw a `TransferException` if a path cannot be resolved by an `IRemotePathResolver` instance.                      | true                                               |

***Note:** API users are strongly encouraged to set `ApplicationName` because the value is included within all log entries.*

### Logging
TAPI supports Relativity Logging and, specifically, the `ILog` object. There may be scenarios, however, where 3rd party developers may wish to use their own logging framework. The `ITransferLog` interface is an extensibility point to address this possible use-case. Similar to other TAPI objects, this interface implements `IDisposable` to manage object life-cycles.

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

#### Log formatting
When a transfer request is made, all log entries are prefixed with useful property values to improve log searching and filtering to a particular request. This includes the following values:

* ApplicationName (via `GlobalSettings`)
* ClientRequestId (via `ITransferRequest`)
* JobId (via `ITransferRequest`)
* Direction (via `ITransferRequest`)

***Note:** The `ApplicationName` should be set to an appropriate value. The value defaults to TAPI if not specified.*

#### Log templates
Relativity Logging uses [Serilog](https://serilog.net/) to format each log entry. Because Relativity Logging is the presumed default, the [Serilog DSL](https://github.com/serilog/serilog/wiki/Writing-Log-Events) is used throughout TAPI. When using a custom `ITransferLog` implementation, the message template and properties must be converted to a string; otherwise, an exception is thrown when calling the String.Format method. [This StackOverflow page](https://stackoverflow.com/questions/26875831/how-do-i-render-a-template-and-its-arguments-manually-in-serilog) provides an example on how to use the `MessageTemplateParser` to convert the Serilog message and properties to a properly formatted string.

### Relativity version check
TAPI relies heavily on Relativity REST endpoints to support many of the standard features provided by the library. For RelativityOne applications, back-end services are continually evolving and increase the likelihood of compatibility issues. Although TAPI doesn't perform an automatic or implicit Relativity version check, a check is available and *should* be called by new applications as early as possible.

If the specified Relativity version is not supported, the method throws `RelativityNotSupportedException` and should be handled accordingly.

```csharp
using (IRelativityTransferHost host = new RelativityTransferHost(connectionInfo))
{
    try
    {
        await host.VersionCheckAsync(token).ConfigureAwait(false);
    }
    catch (RelativityNotSupportedException)
    {
        // Handle the exception.
    }
}
```

Alternatively, a static method exists within the `RelativityTransferHost` class to perform the same test without requiring an `IRelativityTransferHost` instance.

```csharp
try
{
    await RelativityTransferHost.VersionCheckAsync(connectionInfo, token).ConfigureAwait(false);
}
catch (RelativityNotSupportedException)
{
    // Handle the exception.
}
```

### DateTime object values 
All `DateTime` objects values used by TAPI are in local time.

### Binding redirect for Json.NET
Relativity and the APIs consumed by Relativity use several different versions of the Newtonsoft.Json library, which can cause assembly version conflict at both build and run time. This is the recommended assembly binding redirect to Newtonsoft.Json version 6.0.0.0 to add to the consuming application (`app.config`) or web (`web.config`) configuration file. It ensures that almost any version of `Newtonsoft.Json` will work with the TAPI:

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
                       <bindingRedirect oldVersion="0.0.0.0-10.0.0.0" newVersion="6.0.0.0" />
                 </dependentAssembly>
           </assemblyBinding>
      </runtime>
</configuration>
```

### Packaging and RCC package library
There may be scenarios where the cost of transferring massive datasets consisting of small files is significantly higher than packaging them locally, transferring, and potentially extracting them on the server (extracting may not be necessary, depending on workflow). There may also be scenarios where the API user would like to alter file metadata, such as filename or file timestamps, before adding the native files to the package container.

TAPI extends the existing plugin-based design with a simple means to create or use package libraries through a familiar and consistent object model. Some of the key features include:
* MEF-based plugin design.
* Package dependencies are *only* required if packaging is used.
* Package files are transferred *as soon as* each package file is completed.
* Package events are exposed through a new package context object.
* **Limited to uploads only.**

#### RCC package library NuGet packages
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

#### RCC format info
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

#### RCC file types
A standard RCC is composed of three file types where each file type is uniquely identified. The SQLCipher database format provides a feature where the first 16 Bytes can be specified.

| RCC File Type             | Description                                                                                                                                                                    |
| --------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| \{filename\}.rcc          | The master RCC database contains metadata and other lookup tables.                                                                                                             |
| \{filename\}-cf.rcc       | The collected file database contains file and item metadata.                                                                                                                   |
| \{filename\}-cf-xxxx.rcc  | The BLOB database contains the raw collected files. A filename scheme includes numbers to support multi-files once the configured maximum file length is exceeded.             |

#### Package example
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
