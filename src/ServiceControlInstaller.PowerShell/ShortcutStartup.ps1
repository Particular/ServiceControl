if ($PSScriptRoot) {
    if ($PSVersionTable.PSVersion.Major -LT 3 ) {
	    Write-Warning "ServiceControl Management Module not loaded - PowerShell 3.0 or greater required"
    }
    else 
    {
	    Set-ExecutionPolicy -Scope Process Undefined -Force
	    if ($(Get-ExecutionPolicy) -eq "Restricted") {
		    Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned -Force
	    }
        Import-Module (Join-Path $PSScriptRoot ServiceControlMgmt.psd1)
	    SC-Help
    }
} else {
    Write-Warning "ServiceControl Management Module not loaded - PowerShell 3.0 or greater required"
}

