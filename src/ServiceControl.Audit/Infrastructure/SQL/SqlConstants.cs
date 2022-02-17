namespace ServiceControl.Audit.Infrastructure.SQL
{
    static class SqlConstants
    {
        public static string QueryKnownEndpoints = @"
SELECT TOP (@PageSize) * FROM [dbo].[KnownEndpoints]
";

        public static string UpdateKnownEndpoint = @"
UPDATE [dbo].[KnownEndpoints]  WITH (UPDLOCK, SERIALIZABLE) 
SET [Name] = @KE_Name,
    [HostId] = @KE_HostId,
    [Host] = @KE_Host,
    [LastSeen] = @KE_LastSeen
WHERE [Id] = @KE_Id
 
IF @@ROWCOUNT = 0
BEGIN
   INSERT INTO [dbo].[KnownEndpoints]
               ([Id]
               ,[Name]
               ,[HostId]
               ,[Host]
               ,[LastSeen])
         VALUES
               (@KE_Id
               ,@KE_Name
               ,@KE_HostId
               ,@KE_Host
               ,@KE_LastSeen)
END
";

        public static string InsertMessageView = @"
BEGIN TRY     
    INSERT INTO [dbo].[MessagesView]
               ([MessageId]
               ,[MessageType]
               ,[IsSystemMessage]
               ,[IsRetried]
               ,[TimeSent]
               ,[ProcessedAt]
               ,[EndpointName]
               ,[EndpointHostId]
               ,[EndpointHost]
               ,[CriticalTime]
               ,[ProcessingTime]
               ,[DeliveryTime]
               ,[ConversationId])
         VALUES
               (@MV_MessageId
               ,@MV_MessageType
               ,@MV_IsSystemMessage
               ,@MV_IsRetried
               ,@MV_TimeSent
               ,@MV_ProcessedAt
               ,@MV_EndpointName
               ,@MV_EndpointHostId
               ,@MV_EndpointHost
               ,@MV_CriticalTime
               ,@MV_ProcessingTime
               ,@MV_DeliveryTime
               ,@MV_ConversationId)
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 2627
    BEGIN
	    THROW
    END
END CATCH
";

        public static string CreateKnownEndpoints = @"
IF (NOT EXISTS (SELECT * 
                 FROM INFORMATION_SCHEMA.TABLES 
                 WHERE TABLE_SCHEMA = 'dbo' 
                 AND  TABLE_NAME = 'KnownEndpoints'))
BEGIN
    CREATE TABLE [dbo].[KnownEndpoints](
	    [Id] [nvarchar](100) NOT NULL,
	    [Name] [nvarchar](100) NOT NULL,
	    [HostId] [uniqueidentifier] NOT NULL,
	    [Host] [nvarchar](100) NOT NULL,
	    [LastSeen] [datetime] NOT NULL,
     CONSTRAINT [PK_KnownEndpoints] PRIMARY KEY CLUSTERED 
    (
	    [Id] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY]
END";

        public static string QueryMessagesView = @"
SELECT messages.*, headers.HeadersText
FROM [dbo].[MessagesView] AS messages
INNER JOIN [dbo].[Headers] AS headers ON messages.[Id] = headers.[ProcessingId]
ORDER BY [@SortColumn] @SortDirection 
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        public static string CreateMessageViewTable = @"
IF (NOT EXISTS (SELECT * 
                 FROM INFORMATION_SCHEMA.TABLES 
                 WHERE TABLE_SCHEMA = 'dbo' 
                 AND  TABLE_NAME = 'MessagesView'))
BEGIN
    CREATE TABLE [dbo].[MessagesView](
	    [Id] [int] IDENTITY(1,1) NOT NULL,
	    [MessageId] [uniqueidentifier] NOT NULL,
	    [MessageType] [nvarchar](500) NOT NULL,
	    [IsSystemMessage] [bit] NOT NULL,
	    [IsRetried] [bit] NOT NULL,
	    [TimeSent] [datetime] NOT NULL,
	    [ProcessedAt] [datetime] NOT NULL,
	    [EndpointName] [nvarchar](100) NOT NULL,
	    [EndpointHostId] [uniqueidentifier] NOT NULL,
	    [EndpointHost] [nvarchar](100) NOT NULL,
	    [CriticalTime] [bigint] NULL,
	    [ProcessingTime] [bigint] NULL,
	    [DeliveryTime] [bigint] NULL,
	    [ConversationId] [nvarchar](100) NOT NULL,
        
        CONSTRAINT [PK_MessagesView] PRIMARY KEY CLUSTERED 
        (
	        [Id] ASC
        )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY]

CREATE UNIQUE NONCLUSTERED INDEX [IX_MessagesView_MessageId] ON [dbo].[MessagesView]
(
	[MessageId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
END";

        public static string CreateHeadersTable = @"
IF (NOT EXISTS (SELECT * 
                 FROM INFORMATION_SCHEMA.TABLES 
                 WHERE TABLE_SCHEMA = 'dbo' 
                 AND  TABLE_NAME = 'Headers'))
BEGIN
    CREATE TABLE [dbo].[Headers](
	    [Id] [int] IDENTITY(1,1) NOT NULL ,
	    [ProcessingId] [uniqueidentifier] NOT NULL,
	    [HeadersText] [nvarchar](max) NOT NULL,
	    [Query] [nvarchar](max) NOT NULL,
        CONSTRAINT [PK_Headers] PRIMARY KEY CLUSTERED 
        (
	        [Id] ASC
        )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

CREATE UNIQUE NONCLUSTERED INDEX [IX_Headers_ProcessingId] ON [dbo].[Headers]
(
	[ProcessingId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]

END";

        public static string InsertHeaders = @"
BEGIN TRY
    INSERT INTO [dbo].[Headers]
               ([ProcessingId]
               ,[HeadersText]
               ,[Query])
         VALUES
               (@MH_ProcessingId
               ,@MH_HeadersText
               ,@MH_Query)
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 2627
    BEGIN
	    THROW
    END
END CATCH
";

        public static string CreateBodiesTable = @"
IF (NOT EXISTS (SELECT * 
                 FROM INFORMATION_SCHEMA.TABLES 
                 WHERE TABLE_SCHEMA = 'dbo' 
                 AND  TABLE_NAME = 'Bodies'))
BEGIN
    CREATE TABLE [dbo].[Bodies](
	    [Id] [int] IDENTITY(1,1) NOT NULL,
	    [MessageId] [nvarchar](100) NOT NULL,
	    [BodyText] [nvarchar](max) NOT NULL,
        CONSTRAINT [PK_Bodies] PRIMARY KEY CLUSTERED 
        (
	        [Id] ASC
        )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

    CREATE NONCLUSTERED INDEX [IX_Bodies_MessageId] ON [dbo].[Bodies](
	    [Id] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
END
";

        public static string InsertBody = @"
BEGIN TRY
    INSERT INTO [dbo].[Bodies] (MessageId, BodyText) VALUES (@MessageId, @BodyText)
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 2627
    BEGIN
	    THROW
    END
END CATCH";

        public static string QueryTotalMessagesViews = "SELECT Count(*) FROM [dbo].[MessagesView] WITH (nolock)";

        public static string BatchMessageInsert = InsertMessageView + InsertHeaders + UpdateKnownEndpoint;
    }
}