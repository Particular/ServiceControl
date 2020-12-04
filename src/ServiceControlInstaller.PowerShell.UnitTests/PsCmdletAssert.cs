/**
 * Copyright 2016 d-fens GmbH
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Threading;

class PsCmdletAssert
{
    private const string POWERSHELL_CMDLET_NAME_FORMATSTRING = "{0}-{1}";

    // it really does not matter which help file name we use so we take this as a default when constructing a CmdletConfigurationEntry
    private const string HELP_FILE_NAME = "Microsoft.Windows.Installer.PowerShell.dll-Help.xml";

    private const string CMDLET_PARAMETER_FORMAT = "{0} {1}";
    private const string SCRIPTBLOCK_DELIMITER = "; ";

    private readonly IEnumerable<SessionStateVariableEntry> variableEntries;

    public string ScriptDefinition { get; set; }

    public PsCmdletAssert()
        : this(new List<SessionStateVariableEntry>())
    {
        // N/A
    }

    public PsCmdletAssert(IDictionary<string, object> variableEntries)
        : this(variableEntries
            .Select(keyValuePair => new SessionStateVariableEntry(keyValuePair.Key, keyValuePair.Value, string.Empty)))
    {
        Contract.Requires(null != variableEntries);
    }

    public PsCmdletAssert(IEnumerable<SessionStateVariableEntry> variableEntries)
    {
        Contract.Requires(null != variableEntries);

        this.variableEntries = variableEntries;
    }

    #region Obsolete Invoke() methods with string parameters

    [Obsolete("Use Invoke() with Dictionary<> parameters instead.")]
    public IList<PSObject> Invoke(Type implementingType, string parameters)
    {
        Contract.Requires(null != implementingType);
        Contract.Requires(!string.IsNullOrWhiteSpace(parameters));
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        return Invoke(new Type[] {implementingType}, parameters, HELP_FILE_NAME, exceptionHandler: null,
            errorHandler: null, scriptDefinition: null);
    }

    [Obsolete("Use Invoke() with Dictionary<> parameters instead.")]
    public IList<PSObject> Invoke(Type[] implementingTypes, string parameters)
    {
        Contract.Requires(null != implementingTypes);
        Contract.Requires(!string.IsNullOrWhiteSpace(parameters));
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        return Invoke(implementingTypes, parameters, HELP_FILE_NAME, exceptionHandler: null, errorHandler: null,
            scriptDefinition: null);
    }

    [Obsolete("Use Invoke() with Dictionary<> parameters instead.")]
    public IList<PSObject> Invoke(Type implementingType, string parameters, string scriptDefinition)
    {
        Contract.Requires(null != implementingType);
        Contract.Requires(!string.IsNullOrWhiteSpace(parameters));
        Contract.Requires(!string.IsNullOrWhiteSpace(scriptDefinition));
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        return Invoke(new Type[] {implementingType}, parameters, HELP_FILE_NAME, exceptionHandler: null,
            errorHandler: null, scriptDefinition: scriptDefinition);
    }

    [Obsolete("Use Invoke() with Dictionary<> parameters instead.")]
    public IList<PSObject> Invoke(Type[] implementingTypes, string parameters, string scriptDefinition)
    {
        Contract.Requires(null != implementingTypes);
        Contract.Requires(!string.IsNullOrWhiteSpace(parameters));
        Contract.Requires(!string.IsNullOrWhiteSpace(scriptDefinition));
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        return Invoke(implementingTypes, parameters, HELP_FILE_NAME, exceptionHandler: null, errorHandler: null,
            scriptDefinition: scriptDefinition);
    }

    [Obsolete("Use Invoke() with Dictionary<> parameters instead.")]
    public IList<PSObject> Invoke(Type implementingType, string parameters, Func<Exception, Exception> exceptionHandler)
    {
        Contract.Requires(null != implementingType);
        Contract.Requires(!string.IsNullOrWhiteSpace(parameters));
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        return Invoke(new Type[] {implementingType}, parameters, HELP_FILE_NAME, exceptionHandler, errorHandler: null,
            scriptDefinition: null);
    }

    [Obsolete("Use Invoke() with Dictionary<> parameters instead.")]
    public IList<PSObject> Invoke(Type[] implementingTypes, string parameters,
        Func<Exception, Exception> exceptionHandler)
    {
        Contract.Requires(null != implementingTypes);
        Contract.Requires(!string.IsNullOrWhiteSpace(parameters));
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        return Invoke(implementingTypes, parameters, HELP_FILE_NAME, exceptionHandler, errorHandler: null,
            scriptDefinition: null);
    }

    [Obsolete("Use Invoke() with Dictionary<> parameters instead.")]
    public IList<PSObject> Invoke(Type implementingType, string parameters, Func<Exception, Exception> exceptionHandler,
        string scriptDefinition)
    {
        Contract.Requires(null != implementingType);
        Contract.Requires(!string.IsNullOrWhiteSpace(parameters));
        Contract.Requires(!string.IsNullOrWhiteSpace(scriptDefinition));
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        return Invoke(new Type[] {implementingType}, parameters, HELP_FILE_NAME, exceptionHandler, errorHandler: null,
            scriptDefinition: scriptDefinition);
    }

    [Obsolete("Use Invoke() with Dictionary<> parameters instead.")]
    public IList<PSObject> Invoke(Type[] implementingTypes, string parameters,
        Func<Exception, Exception> exceptionHandler, string scriptDefinition)
    {
        Contract.Requires(null != implementingTypes);
        Contract.Requires(!string.IsNullOrWhiteSpace(parameters));
        Contract.Requires(!string.IsNullOrWhiteSpace(scriptDefinition));
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        return Invoke(implementingTypes, parameters, HELP_FILE_NAME, exceptionHandler, errorHandler: null,
            scriptDefinition: scriptDefinition);
    }

    [Obsolete("Use Invoke() with Dictionary<> parameters instead.")]
    public IList<PSObject> Invoke(Type implementingType, string parameters, Func<Exception, Exception> exceptionHandler,
        Action<IList<ErrorRecord>> errorHandler)
    {
        Contract.Requires(null != implementingType);
        Contract.Requires(!string.IsNullOrWhiteSpace(parameters));
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        return Invoke(new Type[] {implementingType}, parameters, HELP_FILE_NAME, exceptionHandler, errorHandler,
            scriptDefinition: null);
    }

    [Obsolete("Use Invoke() with Dictionary<> parameters instead.")]
    public IList<PSObject> Invoke(Type[] implementingTypes, string parameters,
        Func<Exception, Exception> exceptionHandler, Action<IList<ErrorRecord>> errorHandler)
    {
        Contract.Requires(null != implementingTypes);
        Contract.Requires(!string.IsNullOrWhiteSpace(parameters));
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        return Invoke(implementingTypes, parameters, HELP_FILE_NAME, exceptionHandler, errorHandler,
            scriptDefinition: null);
    }

    [Obsolete("Use Invoke() with Dictionary<> parameters instead.")]
    public IList<PSObject> Invoke(Type implementingType, string parameters, Func<Exception, Exception> exceptionHandler,
        Action<IList<ErrorRecord>> errorHandler, string scriptDefinition)
    {
        Contract.Requires(null != implementingType);
        Contract.Requires(!string.IsNullOrWhiteSpace(parameters));
        Contract.Requires(!string.IsNullOrWhiteSpace(scriptDefinition));
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        return Invoke(new Type[] {implementingType}, parameters, HELP_FILE_NAME, exceptionHandler, errorHandler,
            scriptDefinition: scriptDefinition);
    }

    [Obsolete("Use Invoke() with Dictionary<> parameters instead.")]
    public IList<PSObject> Invoke(Type[] implementingTypes, string parameters,
        Func<Exception, Exception> exceptionHandler, Action<IList<ErrorRecord>> errorHandler, string scriptDefinition)
    {
        Contract.Requires(null != implementingTypes);
        Contract.Requires(!string.IsNullOrWhiteSpace(parameters));
        Contract.Requires(!string.IsNullOrWhiteSpace(scriptDefinition));
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        return Invoke(implementingTypes, parameters, HELP_FILE_NAME, exceptionHandler, errorHandler,
            scriptDefinition: scriptDefinition);
    }

    [Obsolete("Use Invoke() with Dictionary<> parameters instead.")]
    public IList<PSObject> Invoke(Type implementingType, string parameters, Action<IList<ErrorRecord>> errorHandler)
    {
        Contract.Requires(null != implementingType);
        Contract.Requires(!string.IsNullOrWhiteSpace(parameters));
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        return Invoke(new Type[] {implementingType}, parameters, HELP_FILE_NAME, exceptionHandler: null,
            errorHandler: errorHandler, scriptDefinition: null);
    }

    [Obsolete("Use Invoke() with Dictionary<> parameters instead.")]
    public IList<PSObject> Invoke(Type[] implementingTypes, string parameters, Action<IList<ErrorRecord>> errorHandler)
    {
        Contract.Requires(null != implementingTypes);
        Contract.Requires(!string.IsNullOrWhiteSpace(parameters));
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        return Invoke(implementingTypes, parameters, HELP_FILE_NAME, exceptionHandler: null, errorHandler: errorHandler,
            scriptDefinition: null);
    }

    [Obsolete("Use Invoke() with Dictionary<> parameters instead.")]
    public IList<PSObject> Invoke(Type implementingType, string parameters, Action<IList<ErrorRecord>> errorHandler,
        string scriptDefinition)
    {
        Contract.Requires(null != implementingType);
        Contract.Requires(!string.IsNullOrWhiteSpace(parameters));
        Contract.Requires(!string.IsNullOrWhiteSpace(scriptDefinition));
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        return Invoke(new Type[] {implementingType}, parameters, HELP_FILE_NAME, exceptionHandler: null,
            errorHandler: errorHandler, scriptDefinition: scriptDefinition);
    }

    [Obsolete("Use Invoke() with Dictionary<> parameters instead.")]
    public IList<PSObject> Invoke(Type[] implementingTypes, string parameters, Action<IList<ErrorRecord>> errorHandler,
        string scriptDefinition)
    {
        Contract.Requires(null != implementingTypes);
        Contract.Requires(!string.IsNullOrWhiteSpace(parameters));
        Contract.Requires(!string.IsNullOrWhiteSpace(scriptDefinition));
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        return Invoke(implementingTypes, parameters, HELP_FILE_NAME, exceptionHandler: null, errorHandler: errorHandler,
            scriptDefinition: scriptDefinition);
    }

    [Obsolete("Use Invoke() with Dictionary<> parameters instead.")]
    public IList<PSObject> Invoke(Type implementingType, string parameters, string helpFileName,
        Func<Exception, Exception> exceptionHandler, Action<IList<ErrorRecord>> errorHandler)
    {
        Contract.Requires(null != implementingType);
        Contract.Requires(!string.IsNullOrWhiteSpace(parameters));
        Contract.Requires(!string.IsNullOrWhiteSpace(helpFileName));
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        return Invoke(new Type[] {implementingType}, parameters, HELP_FILE_NAME, exceptionHandler: null,
            errorHandler: errorHandler, scriptDefinition: null);
    }

    [Obsolete("Use Invoke() with Dictionary<> parameters instead.")]
    public IList<PSObject> Invoke(Type[] implementingTypes, string parameters, string helpFileName,
        Func<Exception, Exception> exceptionHandler, Action<IList<ErrorRecord>> errorHandler)
    {
        Contract.Requires(null != implementingTypes);
        Contract.Requires(!string.IsNullOrWhiteSpace(parameters));
        Contract.Requires(!string.IsNullOrWhiteSpace(helpFileName));
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        return Invoke(implementingTypes, parameters, HELP_FILE_NAME, exceptionHandler: null, errorHandler: errorHandler,
            scriptDefinition: null);
    }

    [Obsolete("Use Invoke() with Dictionary<> parameters instead.")]
    public IList<PSObject> Invoke(Type implementingType, string parameters, string helpFileName,
        Func<Exception, Exception> exceptionHandler, Action<IList<ErrorRecord>> errorHandler, string scriptDefinition)
    {
        Contract.Requires(null != implementingType);
        Contract.Requires(!string.IsNullOrWhiteSpace(parameters));
        Contract.Requires(!string.IsNullOrWhiteSpace(helpFileName));
        Contract.Requires(!string.IsNullOrWhiteSpace(scriptDefinition));
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        return Invoke(new Type[] {implementingType}, parameters, HELP_FILE_NAME, exceptionHandler: exceptionHandler,
            errorHandler: errorHandler, scriptDefinition: scriptDefinition);
    }

    [Obsolete("Use Invoke() with Dictionary<> parameters instead.")]
    public IList<PSObject> Invoke(Type[] implementingTypes, string parameters, string helpFileName,
        Func<Exception, Exception> exceptionHandler, Action<IList<ErrorRecord>> errorHandler, string scriptDefinition)
    {
        Contract.Requires(null != implementingTypes);
        Contract.Requires(!string.IsNullOrWhiteSpace(parameters));
        Contract.Requires(!string.IsNullOrWhiteSpace(helpFileName));
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        var cmdletNameToInvoke = "";

        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Variables.Add(variableEntries);

        foreach (var implementingType in implementingTypes)
        {
            // construct the Cmdlet name the type implements
            var cmdletAttribute =
                (CmdletAttribute) implementingType.GetCustomAttributes(typeof(CmdletAttribute), true).Single();
            Contract.Assert(null != cmdletAttribute, typeof(CmdletAttribute).FullName);
            var cmdletName = string.Format(POWERSHELL_CMDLET_NAME_FORMATSTRING, cmdletAttribute.VerbName,
                cmdletAttribute.NounName);

            if (implementingType == implementingTypes[0])
            {
                cmdletNameToInvoke = cmdletName;
            }

            Contract.Assert(!string.IsNullOrWhiteSpace(cmdletNameToInvoke));

            var sessionStateCommandEntries = new List<SessionStateCommandEntry>
            {
                new SessionStateCmdletEntry(cmdletName, implementingType, helpFileName)
            };
            initialSessionState.Commands.Add(sessionStateCommandEntries);
        }

        using (var runspace = RunspaceFactory.CreateRunspace(initialSessionState))
        {
            runspace.ApartmentState = ApartmentState.STA;
            runspace.Open();

            // add scripts to cmdlet to be executed
            var commandText = new StringBuilder();
            commandText.Append(ScriptDefinition);
            commandText.AppendLine(SCRIPTBLOCK_DELIMITER);
            commandText.Append(scriptDefinition);
            commandText.AppendLine(SCRIPTBLOCK_DELIMITER);
            commandText.AppendFormat(CMDLET_PARAMETER_FORMAT, cmdletNameToInvoke, parameters);
            commandText.AppendLine(SCRIPTBLOCK_DELIMITER);

            using (var pipeline = runspace.CreatePipeline(commandText.ToString()))
            {
                try
                {
                    var invocationResults = pipeline.Invoke();

                    if (null != errorHandler && pipeline.HadErrors)
                    {
                        var errorRecords = pipeline.Error.ReadToEnd().Cast<PSObject>().Select(e => e.BaseObject)
                            .Cast<ErrorRecord>().ToList();
                        errorHandler(errorRecords);
                    }

                    return invocationResults.ToList();
                }
                catch (CmdletInvocationException ex)
                {
                    if (null == exceptionHandler || null == ex.InnerException)
                    {
                        // throw original exception if no handler present
                        throw;
                    }

                    throw exceptionHandler(ex.InnerException);
                }
            }
        }
    }

    #endregion

    #region Invoke with Dictionary parameter stubs

    public IList<PSObject> Invoke(Type cmdletType, Dictionary<string, object> parameters)
    {
        Contract.Requires(null != cmdletType);
        Contract.Requires(null != parameters);
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        return Invoke(cmdletType, parameters, HELP_FILE_NAME, exceptionHandler: null, errorHandler: null);
    }

    public IList<PSObject> Invoke(Type cmdletType, Dictionary<string, object> parameters,
        Func<Exception, Exception> exceptionHandler)
    {
        Contract.Requires(null != cmdletType);
        Contract.Requires(null != parameters);
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        return Invoke(cmdletType, parameters, HELP_FILE_NAME, exceptionHandler, errorHandler: null);
    }

    public IList<PSObject> Invoke(Type cmdletType, Dictionary<string, object> parameters,
        Func<Exception, Exception> exceptionHandler, Action<IList<ErrorRecord>> errorHandler)
    {
        Contract.Requires(null != cmdletType);
        Contract.Requires(null != parameters);
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        return Invoke(cmdletType, parameters, HELP_FILE_NAME, exceptionHandler, errorHandler);
    }


    public IList<PSObject> Invoke(Type cmdletType, Dictionary<string, object> parameters,
        Action<IList<ErrorRecord>> errorHandler)
    {
        Contract.Requires(null != cmdletType);
        Contract.Requires(null != parameters);
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        return Invoke(cmdletType, parameters, HELP_FILE_NAME, exceptionHandler: null, errorHandler: errorHandler);
    }

    #endregion

    public IList<PSObject> Invoke(Type cmdletType, Dictionary<string, object> parameters, string helpFileName,
        Func<Exception, Exception> exceptionHandler, Action<IList<ErrorRecord>> errorHandler)
    {
        Contract.Requires(null != cmdletType);
        Contract.Requires(null != parameters);
        Contract.Requires(!string.IsNullOrWhiteSpace(helpFileName));
        Contract.Ensures(null != Contract.Result<IList<PSObject>>());

        // construct the Cmdlet name the type implements
        var cmdletAttribute = (CmdletAttribute) cmdletType.GetCustomAttributes(typeof(CmdletAttribute), true).Single();
        Contract.Assert(null != cmdletAttribute, typeof(CmdletAttribute).FullName);
        var cmdletName = string.Format(POWERSHELL_CMDLET_NAME_FORMATSTRING, cmdletAttribute.VerbName,
            cmdletAttribute.NounName);

        var cmdletNameToInvoke = cmdletName;
        Contract.Assert(!string.IsNullOrWhiteSpace(cmdletNameToInvoke));

        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Variables.Add(variableEntries);

        var sessionStateCommandEntries = new List<SessionStateCommandEntry>
        {
            new SessionStateCmdletEntry(cmdletName, cmdletType, helpFileName)
        };
        initialSessionState.Commands.Add(sessionStateCommandEntries);

        using (var runspace = RunspaceFactory.CreateRunspace(initialSessionState))
        {
            runspace.ApartmentState = ApartmentState.STA;
            runspace.Open();

            using (var pipeline = runspace.CreatePipeline())
            {
                try
                {
                    var command = new Command(cmdletNameToInvoke);
                    foreach (var parameter in parameters)
                    {
                        command.Parameters.Add(new CommandParameter(parameter.Key, parameter.Value));
                    }

                    pipeline.Commands.Add(command);

                    var invocationResults = pipeline.Invoke();

                    if (null != errorHandler && pipeline.HadErrors)
                    {
                        var errorRecords = pipeline.Error.ReadToEnd().Cast<PSObject>().Select(e => e.BaseObject)
                            .Cast<ErrorRecord>().ToList();
                        errorHandler(errorRecords);
                    }

                    return invocationResults.ToList();
                }
                catch (CmdletInvocationException ex)
                {
                    if (null == exceptionHandler || null == ex.InnerException)
                    {
                        // throw original exception if no handler present
                        throw;
                    }

                    throw exceptionHandler(ex.InnerException);
                }
            }
        }
    }

    public void HasAlias(Type implementingType, string expectedAlias)
    {
        Contract.Requires(null != implementingType);
        Contract.Requires(!string.IsNullOrWhiteSpace(expectedAlias));

        HasAlias(implementingType, expectedAlias, null);
    }

    public void HasAlias(Type implementingType, string expectedAlias, string message)
    {
        Contract.Requires(null != implementingType);
        Contract.Requires(!string.IsNullOrWhiteSpace(expectedAlias));

        var customAttribute =
            (AliasAttribute) implementingType.GetCustomAttributes(typeof(AliasAttribute), true).FirstOrDefault();
        var isAttributeDefined = null != customAttribute?.AliasNames;
        if (!isAttributeDefined)
        {
            var attributeNotDefinedMessage = new StringBuilder();
            attributeNotDefinedMessage.AppendFormat(
                "PsCmdletAssert2.IsAliasDefined FAILED. No AliasAttribute defined.");
            if (null != message)
            {
                attributeNotDefinedMessage.AppendFormat(" '{0}'", message);
            }

            throw new Exception(attributeNotDefinedMessage.ToString());
        }

        var isAliasDefined = customAttribute.AliasNames.Any(expectedAlias.Equals);
        if (isAliasDefined)
        {
            return;
        }

        var aliasNotDefinedMessage = new StringBuilder();
        aliasNotDefinedMessage.AppendFormat("PsCmdletAssert2.IsAliasDefined FAILED. ExpectedAlias '{0}' not defined.",
            expectedAlias);
        if (null != message)
        {
            aliasNotDefinedMessage.AppendFormat(" '{0}'", message);
        }

        throw new Exception(aliasNotDefinedMessage.ToString());
    }

    public void HasOutputType(Type implementingType, Type expectedOutputType)
    {
        Contract.Requires(null != implementingType);
        Contract.Requires(null != expectedOutputType);

        HasOutputType(implementingType, expectedOutputType.FullName, ParameterAttribute.AllParameterSets, null);
    }

    public void HasOutputType(Type implementingType, string expectedOutputTypeName)
    {
        Contract.Requires(null != implementingType);
        Contract.Requires(!string.IsNullOrWhiteSpace(expectedOutputTypeName));

        HasOutputType(implementingType, expectedOutputTypeName, ParameterAttribute.AllParameterSets, null);
    }

    public void HasOutputType(Type implementingType, Type expectedOutputType, string parameterSetName)
    {
        Contract.Requires(null != implementingType);
        Contract.Requires(null != expectedOutputType);

        HasOutputType(implementingType, expectedOutputType.FullName, parameterSetName, null);
    }

    public void HasOutputType(Type implementingType, string expectedOutputTypeName, string parameterSetName)
    {
        Contract.Requires(null != implementingType);
        Contract.Requires(!string.IsNullOrWhiteSpace(expectedOutputTypeName));

        HasOutputType(implementingType, expectedOutputTypeName, parameterSetName, null);
    }

    public void HasOutputType(Type implementingType, string expectedOutputTypeName, string parameterSetName,
        string message)
    {
        Contract.Requires(null != implementingType);
        Contract.Requires(!string.IsNullOrWhiteSpace(expectedOutputTypeName));
        Contract.Requires(!string.IsNullOrWhiteSpace(parameterSetName));

        var outputTypeAttributes =
            (OutputTypeAttribute[]) implementingType.GetCustomAttributes(typeof(OutputTypeAttribute), true);

        var isValidOutputType = false;

        var outputTypeAttributesForGivenParameterSetName =
            outputTypeAttributes.Where(e => e.ParameterSetName.Contains(parameterSetName));
        foreach (var outputTypeAttribute in outputTypeAttributesForGivenParameterSetName)
        {
            isValidOutputType |= outputTypeAttribute.Type.Any(e => e.Name == expectedOutputTypeName);
        }

        if (isValidOutputType)
        {
            return;
        }

        var outputTypeAttributesForAllParameterSets =
            outputTypeAttributes.Where(e => e.ParameterSetName.Contains(ParameterAttribute.AllParameterSets));
        foreach (var outputTypeAttribute in outputTypeAttributesForAllParameterSets)
        {
            isValidOutputType |= outputTypeAttribute.Type.Any(e => e.Name == expectedOutputTypeName);
        }

        if (isValidOutputType)
        {
            return;
        }

        var invalidOutputTypeMessage = new StringBuilder();
        invalidOutputTypeMessage.AppendFormat(
            "PsCmdletAssert2.HasOutputType FAILED. ExpectedType '{0}' not defined for ParameterSetName '{1}'.",
            expectedOutputTypeName, parameterSetName);
        if (null != message)
        {
            invalidOutputTypeMessage.AppendFormat(" '{0}'", message);
        }

        throw new Exception(invalidOutputTypeMessage.ToString());
    }
}