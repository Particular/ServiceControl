<# 
	.Synopsis
	Get an initialized Installer 
#>
Function InstallerFactory {
	PROCESS {
		$zipfolder =  (Get-item (Get-Module -Name "ServiceControlMgmt").Path).DirectoryName
		$logger = new-object ServiceControlInstaller.Engine.Powershell.PSLogger($host)
		New-Object ServiceControlInstaller.Engine.UnattendInstaller($logger, $zipfolder)
	}
}

<#
  System.IO.Path.Combine has two properties making it necesarry here:
    1) correctly deals with situations where $Path (the second term) is an absolute path
    2) correctly deals with situations where $Path (the second term) is relative
  (join-path) commandlet does not have this first property
#>
function Get-AbsolutePath ($Path)
{
    $Path = [System.IO.Path]::Combine(((pwd).Path), ($Path))
    $Path = [System.IO.Path]::GetFullPath($Path)
    return $Path
}

<# 
    .Synopsis
    List helpful info about transport types

    .Description
	List helpful info about transport types which can be used when setting config info

	.Example
	# List 
	Get-ServiceControlTransportTypes

	.Example
	# List with more detail
	Get-ServiceControlTransportTypes | Select -Property * 
#>
Function Get-ServiceControlTransportTypes {
    PROCESS {
		$typeName = 'ServiceControlInstaller.Engine.Instances.TransportInfo'
		Update-TypeData -TypeName $typeName -DefaultDisplayPropertySet Name,TypeName -ErrorAction SilentlyContinue
        [ServiceControlInstaller.Engine.Instances.Transports]::All 
    }
}

<# 
    .Synopsis
    List the currently configured ServiceControl instances

    .Description
    List the currently configured ServiceControl instances

    .Example
	# List All
	Get-ServiceControlInstances 
#>
Function Get-ServiceControlInstances {
    PROCESS {
        [ServiceControlInstaller.Engine.Instances.ServiceControlInstance]::Instances()
    }
}

<# 
    .Synopsis
	Create an instance from an unattended install file

    .Description
    Create an instance from an unattended install file. Useful for testing the unattended file without re-running the full installer
    
	.ParameterUnattended File
	The unattended xml file to process

	.Example
	Import-ServiceControlInstanceFromUnattendedFile -unattendedfile sample.xml
    
#>
Function Import-ServiceControlInstanceFromUnattendedFile {
    
    PARAM(
        [Parameter(Mandatory,ValueFromPipeline,ValueFromPipelineByPropertyName)]
		[alias("FullName")]
        [ValidateNotNull()]
	    [string] $UnattendFile
    )

    BEGIN {
        Test-IfAdmin 
    }

    PROCESS {

		$fullunattendfilename = Get-AbsolutePath $UnattendFile

        if (-not (Test-Path -Path $fullunattendfilename -PathType Leaf)) {
            throw ("Could not find file - {0}" -f $fullunattendfilename)
        }

        $details = [ServiceControlInstaller.Engine.Instances.ServiceControlInstanceMetadata]::Load($fullunattendfilename)
		$installer = InstallerFactory
		$installer.Add($details)
    }
}

<# 
    .Synopsis
    Removes a instance of ServiceControl

    .Description
    Removes a instance of ServiceControl and optionally attempts to cleanup the logs and db folders

	.Parameter Name
	The name of the instance as lists when using Get-ServiceControlInstances. The instancename is also identical to
	the Windows Service name (not the Windows Service Display Name)

	.Parameter RemoveDB
	Attempt to remove the RavenDB folder and all contents. If this fails a warning will be shown

	.Parameter RemoveLogs
	Attempt to remove the Logs folder and all contents. If this fails a warning will be shown

	.Example
	Remove-ServiceControlInstance -InstanceName Particular.ServiceControl -RemoveDB -RemoveLogs
#>
Function Remove-ServiceControlInstance {
    PARAM(
		[Parameter(Mandatory,ValueFromPipeline,ValueFromPipelineByPropertyName)][ValidateNotNull()] [string] $Name,
		[switch] $RemoveDb,
		[switch] $RemoveLogs
	)

    BEGIN {
        Test-IfAdmin 
    }
    PROCESS {
		$instance = Get-ServiceControlInstances | ? Name -eq $Name
		if ($instance) {
			$installer = InstallerFactory
			$instanceName = $instance.Name
			$installer.Delete($instance.Name, $Removedb.IsPresent, $RemoveLogs.IsPresent)
		} else {
			Write-Warning ("No action taken. An instance called {0} was not found" -f $Name)
		}
    }
}

<# 
    .Synopsis
    Add a new instance of ServiceControl

    .Description
    Add a new instance of ServiceControl

    .Parameter Name
    The name used for the Windows Service Intance

    .Parameter InstallPath
    Location for the binaries for this service instance 

    .Parameter DBPath
    The directory where RavenDB will be written for this instance

    .Parameter LogPath
    The directory where logs files will be written for this instance
	
	.Parameter HostName
	The host name used in the URLACL configuration for this service. Default is 'localhost'
	
    .Parameter Port
	The port number this service with use. If this is your only instance of ServiceControl use 33333
    as this is what ServicePulse and ServiceInsight will expect 

    .Parameter ErrorQueue
    The Error Queue you wish ServiceControl to consume. Default is 'error'

    .Parameter AuditQueue
    The Audit Queue you wish ServiceControl to consume. Default is 'audit'

    .Parameter ErrorLogQueue
    This queue will be created by ServiceControl and all consumed error messages are forwarded to it. Default is 'errorlog'

    .Parameter AuditLogQueue
    This queue will be created by ServiceControl and all consumed audit messages are forwarded to it if ForwardAuditMessages is enabled. Default is 'auditlog'

    .Parameter ForwardAuditMessages
    This switch enables the forwarding of consumed audit messages to the queue specified by AuditLogQueue

    .Parameter Transport
    Specifies the Transport files to install. Valid options are AzureServiceBus,AzureStorageQueue, MSMQ, SQLServer and RabbitMQ

    .Parameter DisplayName
    Set the display name on the Windows Service, if not set the Name will be used

    .Parameter ConnectionString
    The transport specific connection string to use to connect to the queueing system
    
	.Parameter VirtualDirectory
    Optional virtualdirectory name to add to the ServiceControl URL


    .Parameter Description
    Set the description on the Windows Service
            
    .Parameter ServiceAccount 
    The Windows Account to use as the ServiceAccount.  Default is LocalSystem

    .Parameter ServiceAccountPassword
    The Password for the Service Account if it has one
#>
Function Add-ServiceControlInstance {
	PARAM(
		[Parameter(Mandatory)] [ValidateNotNull()] [string] $Name,
		[Parameter(Mandatory)] [ValidateNotNull()] [string] $InstallPath,
		[Parameter(Mandatory)] [ValidateNotNull()] [string] $DBPath, 
		[Parameter(Mandatory)] [ValidateNotNull()] [string] $LogPath, 
		[ValidateNotNullOrEmpty()][string] $HostName = "localhost",
		[Parameter(Mandatory)][ValidateRange(1,49151)][int] $Port, 
		[ValidateNotNullOrEmpty()][string] $ErrorQueue = "error",
		[ValidateNotNullOrEmpty()][string] $AuditQueue = "audit", 
		[ValidateNotNullOrEmpty()][string] $ErrorLogQueue = "errorlog",
		[ValidateNotNullOrEmpty()][string] $AuditLogQueue = "auditlog",
		[Parameter(Mandatory)] [ValidateSet("AzureServiceBus","AzureStorageQueue","MSMQ","SQLServer","RabbitMQ")] $Transport,
		[string] $DisplayName,
		[string] $ConnectionString,
		[string] $VirtualDirectory,
		[string] $Description = "",
		[switch] $ForwardAuditMessages,
		[string] $ServiceAccount = "LocalSystem",
		[string] $ServiceAccountPassword
    )

    BEGIN {
        Test-IfAdmin 
    }
    PROCESS {

		$details = new-object ServiceControlInstaller.Engine.Instances.ServiceControlInstanceMetadata
        
		<# paths #>

		$details.InstallPath = Get-AbsolutePath $InstallPath
		$details.LogPath = Get-AbsolutePath $LogPath
		$details.DBPath = Get-AbsolutePath $DBPath
            
		<# service details #>

		$details.Name = $Name
		if ([String]::IsNullOrWhiteSpace($DisplayName)) {
			$details.DisplayName = $Name
		} else {
			$details.DisplayName = $DisplayName
		}

		$details.ServiceAccount = $ServiceAccount
		$details.ServiceAccountPwd = $ServiceAccountPassword
		$details.ServiceDescription = $ServiceDescription

		<# config details #>

		$details.HostName = $HostName
		$details.Port = $Port
		$details.VirtualDirectory = $VirtualDirectory

		$details.AuditLogQueue = $AuditLogQueue
		$details.AuditQueue = $AuditQueue
		$details.ErrorLogQueue = $ErrorLogQueue
		$details.ErrorQueue =$ErrorQueue
		$details.ForwardAuditMessages = $ForwardAuditMessages.IsPresent 

		$details.ConnectionString = $ConnectionString
		$details.TransportPackage = $Transport
    
		<# Run  Install #>
		$installer = InstallerFactory
		$installer.Add($details)
    }
}

<# 
    .Synopsis
    Upgrade an instance of ServiceControl to a new version

    .Description
	Upgrade an instance of ServiceControl to a new version
    
    .Parameter Name
	The name of the ServiceControl Instance to target. This is the Instance Name as shown
	in the management tool or via the Get-ServiceControlInstances cmdlet
    
    .Example
	Invoke-ServiceControlInstanceUpgrade -InstanceName Particular.ServiceControl
#>
Function Invoke-ServiceControlInstanceUpgrade {
    PARAM(
		[Parameter(Mandatory,ValueFromPipeline,ValueFromPipelineByPropertyName)][ValidateNotNull()] [string] $Name
    )

    BEGIN {
        Test-IfAdmin 
    }
    PROCESS {
		$installer = InstallerFactory
        $instance = Get-ServiceControlInstances | ? Name -eq $Name
		if ($instance) {
			$installer.Upgrade($instance) 
		}
		else {
			Write-Warning ("No action taken. An instance called {0} was not found" -f $Name)
		}

    }
}

<# 
    .Synopsis
    Lists the commands and aliases provided by the ServiceControlMgmt Module.
    
	.Description
	Lists the commands and aliases provided by the ServiceControlMgmt Module.  This command is ment
	as a starting point as it list what is available in the module.
#>
Function Get-ServiceControlMgmtCommands {
    PROCESS {
		Write-Verbose "The following commands are available:" -verbose
        Get-Alias -Scope Global | ? ModuleName -EQ "ServiceControlMgmt" | Select-Object -Property @{Name = 'Alias'; Expression = {$_.Name}}, @{Name = 'Command'; Expression = {$_.Definition}}
		Write-Verbose "Use Get-Help <command|alias> for further information" -verbose
    }
}

<# 
    .Synopsis
    List all registered URLACls. 

    .Description
    List all registered URLACls. The listed entries can be used to diagnose URLACL issues which
    may prevent ServiceControl from starting. If you require a more feature rich tool for managing
    URLACLs please take a look at NetSh.exe

    .Notes
    urlacl-list is an alias for this function

    .Example
    # List all Entries
    Get-UrlAcls

    .Example
    # Find the URLACLs for port 33333 (Default ServiceControl Port)
    Get-UrlAcls | ? Port -EQ 33333

    .Example
    # List all Entries using the alias
    urlacl-list | ? Port -EQ 33333

    .Link
    https://msdn.microsoft.com/en-us/library/windows/desktop/cc307236%28v=vs.85%29.aspx
#>
Function Get-URLACLs {

    PROCESS {
        [ServiceControlInstaller.Engine.UrlAcl.UrlReservation]::GetAll() 
    }
}

<# 
    .Synopsis
    Remove a URLACL
    .Description
    Deletes the registered URLACl from the system. The command will takes URlReservation objects (See Get-URLACL
    .Parameter urlacl 
    Url reservation to remove. Use one or more from Get-URLACLs
    .Notes
    This command is aliases to urlacl-remove

    .Example
    # Remove all URLACLs assigned to port 33333
    Get-UrlAcls | ? Port -EQ 33333 | Remove-UrlACl

    .Example
    # Remove all URLACLs assigned to port 33333 (using the aliases)
    urlacl-list | ? Port -EQ 33338 | urlacl-remove

    .Link
    https://msdn.microsoft.com/en-us/library/windows/desktop/cc307236%28v=vs.85%29.aspx
#>
Function Remove-URLACL {
    PARAM(
        [Parameter(Mandatory,ValueFromPipeline)]
        [ValidateNotNull()]
	    [ServiceControlInstaller.Engine.UrlAcl.UrlReservation[]] $urlacl
    )

    BEGIN {
        Test-IfAdmin
    }

    PROCESS {
        foreach ($item in $urlacl) {
            [ServiceControlInstaller.Engine.UrlAcl.UrlReservation]::Delete($item)
        }
    }
}

<# 
    .Synopsis
    Add a URLACl reservation to the system

    .Description
    Adds a URLACl reservation to the system, this action requires Admin privileges. 
    This can be achieved by using NetSh.exe as well (See related links)

    .Parameter url
    The Url parameter must have a trailing slash to be considered valid

    .Parameter users
    Typically the users parameter is a single user or group

    .Example
    # Add a URL ACL for the ServiceControl 
    Add-UrlAcl -url http://localhost:33333/api/ -users Builtin\Users

    .Example
    # Add a URL ACL for the ServiceControl using the alias
    urlacl-add -url http://localhost:33333/api/ -users Builtin\Users

    .Notes
    This command is aliases to urlacl-add 

    .Link
    https://msdn.microsoft.com/en-us/library/windows/desktop/cc307236%28v=vs.85%29.aspx
#>
Function Add-URLACL {
    PARAM(
        [Parameter(Mandatory)] [ValidateNotNull()] [string] $url,
	    [string[]] $users
    )

    BEGIN {
        Test-IfAdmin
    }
    
    PROCESS {
        $securityIDs  = $users | Get-SecurityIdentifier
        $urlacl = New-Object ServiceControlInstaller.Engine.UrlAcl.UrlReservation($url, $securityIDs) 
        [ServiceControlInstaller.Engine.UrlAcl.UrlReservation]::Create($urlacl)
    }
}

<# 
    .Synopsis
    Tests if the current user has admin privileges

    .Description
    Tests if the current user has admin privileges. If not it throws an exception. 
    This method is used in several other cmdlets to test for admin privileges prior to execution
#>
Function Test-IfAdmin {

    PROCESS {

        $isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] 'Administrator')
        if(!$isAdmin) {
            throw "You must have administrative permissions to use this method";
        }
		Write-Verbose "You are running with administrative permissions" 
    }
}

<# 
    .Synopsis
    Get the SecurityIdentifier (SID) for a User or Group

    .Description
    Get the SecurityIdentifier (SID) for a User or Group.  The cmdlet will process one or more users if passed an array. 

    .Parameter username
    Username can be the short name for a local user. e.g Administator
    Or Username can specified in the longer form which specifies a user belonging
    to a domain or computer. e.g. MyComputer\Administrator

    .Example
    # Get the sid for the Users group
    Get-SecurityIdentifier Builtin\Users

    .Example
    # Get the sid for the NetworkService Account
    Get-SecurityIdentifier NetworkService

    .Example
    # Get the sid for the LocalSystem Account
    Get-SecurityIdentifier System
#>
Function Get-SecurityIdentifier {
    PARAM(
        [Parameter(Mandatory, ValueFromPipeline)] [ValidateNotNull()] [string[]] $UserName
    )

    PROCESS {
        foreach ($item in $UserName)  {
            $ntaccount = New-Object System.Security.Principal.NTAccount($item)
            $ntaccount.Translate([System.Security.Principal.SecurityIdentifier])
        }
    }
}

<# 
    .Synopsis
    Check a TCP port number to see if it is already in use

    .Description
	Check a TCP port number to see if it is already in use
    
    .Parameter Port
    The port number to check. Must be between 1 and 65535

    .Example
    # Get the sid for the Users group
    Test-IfPortIsAvailable -Port 33333
#>
Function Test-IfPortIsAvailable {

	PARAM(
        [Parameter(Mandatory, ValueFromPipeline)]
        [ValidateRange(1, 65535)]
	    [int[]] $Port
    )

    PROCESS {

		# Set Display Order using a custom type
		$typeName = 'PortAvailability.Information'
		Update-TypeData -TypeName $typeName -DefaultDisplayPropertySet Port,Available -ErrorAction SilentlyContinue
		
        foreach ($item in $Port)  {
			$details = @{ 
				"Port" = $item
				"Available" = [ServiceControlInstaller.Engine.Ports.PortUtils]::IsAvailable($item) 
			}
			$object = new-object PSObject -Property $details
			$object.PSTypeNames.Insert(0, $typeName)
			$object 
        }
    }
}

<# 
    .Synopsis
    Look for ServiceControl Licenses

    .Description
    Looks for existing ServiceControl License(s) and displays info about it. 

    .Notes
    sc-findlicense is an alias for this function
#>
Function Get-ServiceControlLicense {

    PROCESS {
        [ServiceControlInstaller.Engine.LicenseMgmt.LicenseManager]::FindLicenses() | Select -Property Location -ExpandProperty Details
    }
}


<# 
    .Synopsis
    Imports the Particular Platform License into the Registry

    .Description
    Imports the Platform License into the Registry for use by ServiceControl

    .Parameter licensefile
    The path to the license file
 
    .Notes
    sc-addlicense is an alias for this function

    .Example
    Import-ServiceControlLicense -licensefile c:\temp\license.xml

    .Example
    sc-addlicense -licensefile c:\temp\license.xml
#>
Function Import-ServiceControlLicense {
 
    PARAM(
        [Parameter(Mandatory,ValueFromPipeline)]
        [ValidateNotNull()]
	    [string] $licensefile
    )

    BEGIN {
        Test-IfAdmin
    }

    PROCESS {

		$fulllicensefilepath = Get-AbsolutePath $licensefile
	    if (-not (Test-Path -Path $fulllicensefilepath -PathType Leaf)) {
            throw ("Could not find license file - {0}" -f $fulllicensefilepath)
        }

		[string] $errorMsg = ""
        if (-not ([ServiceControlInstaller.Engine.LicenseMgmt.LicenseManager]::TryImportLicense($fulllicensefilepath, [ref] $errorMsg))) {
            throw $errorMsg
        }
    }
}

<# 
    .Synopsis
    Make a new unnattended response file

    .Description
    Make a new unnattended response file, This file can be used to create an instance as part of the install.
	If an instance of the same name exists it will be

    .Parameter Name
    The name used for the Windows Service Intance

    .Parameter InstallPath
    Location for the binaries for this service instance 

    .Parameter DBPath
    The directory where RavenDB will be written for this instance

    .Parameter LogPath
    The directory where logs files will be written for this instance
	
	.Parameter HostName
	The host name used in the URLACL configuration for this service. Default is 'localhost'
	
    .Parameter Port
	The port number this service with use. If this is your only instance of ServiceControl use 33333
    as this is what ServicePulse and ServiceInsight will expect 

    .Parameter ErrorQueue
    The Error Queue you wish ServiceControl to consume. Default is 'error'

    .Parameter AuditQueue
    The Audit Queue you wish ServiceControl to consume. Default is 'audit'

    .Parameter ErrorLogQueue
    This queue will be created by ServiceControl and all consumed error messages are forwarded to it. Default is 'errorlog'

    .Parameter AuditLogQueue
    This queue will be created by ServiceControl and all consumed audit messages are forwarded to it if ForwardAuditMessages is enabled. Default is 'auditlog'

    .Parameter ForwardAuditMessages
    This switch enables the forwarding of consumed audit messages to the queue specified by AuditLogQueue

    .Parameter Transport
    Specifies the Transport files to install. Valid options are AzureServiceBus,AzureStorageQueue, MSMQ, SQLServer and RabbitMQ

    .Parameter DisplayName
    Set the display name on the Windows Service, if not set the Name will be used

    .Parameter ConnectionString
    The transport specific connection string to use to connect to the queueing system
    
	.Parameter VirtualDirectory
    Optional virtualdirectory name to add to the ServiceControl URL


    .Parameter Description
    Set the description on the Windows Service
            
    .Parameter ServiceAccount 
    The Windows Account to use as the ServiceAccount.  Default is LocalSystem

    .Parameter ServiceAccountPassword
    The Password for the Service Account if it has one

	.Parameter OutputFile
    Path of the file to save the settings to.
#>
Function New-ServiceControlUnattendedFile {
	PARAM(
		[Parameter(Mandatory)] [ValidateNotNull()] [string] $Name,
		[Parameter(Mandatory)] [ValidateNotNull()] [string] $InstallPath,
		[Parameter(Mandatory)] [ValidateNotNull()] [string] $DBPath, 
		[Parameter(Mandatory)] [ValidateNotNull()] [string] $LogPath, 
		[ValidateNotNullOrEmpty()][string] $HostName = "localhost",
		[Parameter(Mandatory)][ValidateRange(1,49151)][int] $Port, 
		[ValidateNotNullOrEmpty()][string] $ErrorQueue = "error",
		[ValidateNotNullOrEmpty()][string] $AuditQueue = "audit", 
		[ValidateNotNullOrEmpty()][string] $ErrorLogQueue = "errorlog",
		[ValidateNotNullOrEmpty()][string] $AuditLogQueue = "auditlog",
		[Parameter(Mandatory)] [ValidateSet("AzureServiceBus","AzureStorageQueue","MSMQ","SQLServer","RabbitMQ")] $Transport,
		[string] $DisplayName,
		[string] $ConnectionString,
		[string] $VirtualDirectory,
		[string] $Description = "",
		[switch] $ForwardAuditMessages,
		[string] $ServiceAccount = "LocalSystem",
		[string] $ServiceAccountPassword,
		[string] $OutputFile 
	)
    PROCESS {
		$details = new-object ServiceControlInstaller.Engine.Instances.ServiceControlInstanceMetadata
        $details.InstallPath =  Get-AbsolutePath $InstallPath
		$details.LogPath = Get-AbsolutePath $LogPath
		$details.DBPath = Get-AbsolutePath $DBPath
            
		$details.Name = $Name
		if ([String]::IsNullOrWhiteSpace($DisplayName)) {
			$details.DisplayName = $Name
		} else {
			$details.DisplayName = $DisplayName
		}

		$details.ServiceAccount = $ServiceAccount
		$details.ServiceAccountPwd = $ServiceAccountPassword
		$details.ServiceDescription = $ServiceDescription

		<# config details #>

		$details.HostName = $HostName
		$details.Port = $Port
		$details.VirtualDirectory = $VirtualDirectory

		$details.AuditLogQueue = $AuditLogQueue
		$details.AuditQueue = $AuditQueue
		$details.ErrorLogQueue = $ErrorLogQueue
		$details.ErrorQueue =$ErrorQueue
		$details.ForwardAuditMessages = $ForwardAuditMessages.IsPresent 

		$details.ConnectionString = $ConnectionString
		$details.TransportPackage = $Transport

		$fullfileoutputpath = Get-AbsolutePath $OutputFile
		$details.Save($fullfileoutputpath)
    }
}

New-Alias    -Value New-ServiceControlUnattendedFile -Name  sc-makeunattendfile
Export-ModuleMember New-ServiceControlUnattendedFile -Alias sc-makeunattendfile

New-Alias    -Value Get-ServiceControlLicense -Name  sc-findlicense 
Export-ModuleMember Get-ServiceControlLicense -Alias sc-findlicense

New-Alias    -Value Import-ServiceControlLicense -Name  sc-addlicense 
Export-ModuleMember Import-ServiceControlLicense -Alias sc-addlicense

New-Alias    -Value Test-IfPortIsAvailable -Name  port-check
Export-ModuleMember Test-IfPortIsAvailable -Alias port-check

New-Alias    -Value Get-SecurityIdentifier -Name  user-sid
Export-ModuleMember Get-SecurityIdentifier -Alias user-sid

New-Alias    -Value Get-UrlAcls -Name  urlacl-list 
Export-ModuleMember Get-UrlAcls -Alias urlacl-list 

New-Alias    -Value Add-UrlAcl -Name  urlacl-add 
Export-ModuleMember Add-UrlAcl -Alias urlacl-add 

New-Alias    -Value Remove-UrlAcl -Name  urlacl-delete
Export-ModuleMember Remove-UrlAcl -Alias urlacl-delete

New-Alias    -Value Get-ServiceControlInstances -Name  sc-instances 
Export-ModuleMember Get-ServiceControlInstances -Alias sc-instances

New-Alias    -Value New-ServiceControlInstanceFromUnattendedFile -Name  sc-addfromunattendfile
Export-ModuleMember New-ServiceControlInstanceFromUnattendedFile -Alias sc-addfromunattendfile

New-Alias    -Value Invoke-ServiceControlInstanceUpgrade -Name  sc-upgrade 
Export-ModuleMember Invoke-ServiceControlInstanceUpgrade -Alias sc-upgrade

New-Alias    -Value Remove-ServiceControlInstance -Name  sc-delete 
Export-ModuleMember Remove-ServiceControlInstance -Alias sc-delete

New-Alias    -Value Add-ServiceControlInstance -Name  sc-add 
Export-ModuleMember Add-ServiceControlInstance -Alias sc-add

New-Alias    -Value Get-ServiceControlMgmtCommands -Name  sc-help
Export-ModuleMember Get-ServiceControlMgmtCommands -Alias sc-help

New-Alias    -Value Get-ServiceControlTransportTypes -Name  sc-transportsinfo
Export-ModuleMember Get-ServiceControlTransportTypes -Alias sc-transportsinfo

<# Init Code #>
$requiredDLL = [AppDomain]::CurrentDomain.GetAssemblies() | Select -ExpandProperty Modules | ? Name -eq ServiceControlInstaller.Engine.dll 
if (!$requiredDLL) {
    throw "This module was imported incorrectly - use 'Import-Module .\ServiceControlMgmt.psd1' to load this module"  
}