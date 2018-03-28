# Win32UWPAppService

Application interaction between desktop Win32 and universal windows platform by App Service

About [App Service](https://docs.microsoft.com/en-us/windows/uwp/launch-resume/app-services)

* AppServiceComponent: UWP library project implemented IBackgroundTask for App Service
* AppServiceProvider: UWP app project implemented App Service declaration
* Win32WPFClient: Traditional .NET project implemented client app connecting to UWP App Service

How to use:
* Open solution file in Visual Studio 2017
* Build the solution
* Deploy AppServiceProvider
* Launch AppServiceProvider and Win32WPFClient
