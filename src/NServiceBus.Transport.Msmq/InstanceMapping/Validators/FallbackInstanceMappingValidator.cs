namespace NServiceBus.Transport.Msmq
{
    using System;
    using System.Xml.Linq;
    using Logging;

    class FallbackInstanceMappingValidator : IInstanceMappingValidator
    {
        public FallbackInstanceMappingValidator(
            IInstanceMappingValidator preferredValidator,
            IInstanceMappingValidator fallbackValidator,
            string fallbackWarning)
        {
            this.preferredValidator = preferredValidator;
            this.fallbackValidator = fallbackValidator;
            this.fallbackWarning = fallbackWarning;
            logWarningOnFallback = true;
        }

        public void Validate(XDocument document)
        {
            try
            {
                preferredValidator.Validate(document);
                logWarningOnFallback = true;
            }
            catch (Exception ex)
            {
                if (logWarningOnFallback)
                {
                    Logger.Warn(fallbackWarning, ex);
                    logWarningOnFallback = false;
                }
                fallbackValidator.Validate(document);
            }
        }

        IInstanceMappingValidator preferredValidator;
        IInstanceMappingValidator fallbackValidator;
        string fallbackWarning;
        bool logWarningOnFallback;

        static ILog Logger = LogManager.GetLogger<FallbackInstanceMappingValidator>();
    }
}