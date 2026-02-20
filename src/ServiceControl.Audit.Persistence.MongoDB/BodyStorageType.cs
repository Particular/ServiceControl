namespace ServiceControl.Audit.Persistence.MongoDB
{
    /// <summary>
    /// Specifies where message bodies should be stored.
    /// </summary>
    public enum BodyStorageType
    {
        /// <summary>
        /// Message bodies are not stored.
        /// </summary>
        None,

        /// <summary>
        /// Message bodies are stored in the MongoDB database.
        /// </summary>
        Database,

        /// <summary>
        /// Message bodies are stored in Azure Blob Storage.
        /// </summary>
        Blob
    }
}
