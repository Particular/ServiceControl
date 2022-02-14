namespace ServiceControl.Audit.Infrastructure.SQL
{
    static class SqlConstants
    {
        public static string QueryKnownEndpoints = @"
SELECT TOP (@PageSize) * FROM [dbo].[KnownEndpoints]
";

        public static string UpdateKnownEndpoint = @"
IF NOT EXISTS (SELECT * FROM [dbo].[KnownEndpoints] 
               WHERE [Id] = @Id)
BEGIN 
    INSERT INTO [dbo].[KnownEndpoints]
               ([Id]
               ,[Name]
               ,[HostId]
               ,[Host]
               ,[LastSeen])
         VALUES
               (@Id
               ,@Name
               ,@HostId
               ,@Host
               ,@LastSeen)
END
ELSE
BEGIN
    UPDATE [dbo].[KnownEndpoints]
         SET [Name] = @Name,
             [HostId] = @HostId,
             [Host] = @Host,
             [LastSeen] = @LastSeen
         WHERE [Id] = @Id
END
";

        public static string InsertMessageView = @"
IF NOT EXISTS (SELECT * FROM [dbo].[MessagesView] 
               WHERE [MessageId] = @MessageId)
BEGIN       
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
               (@MessageId
               ,@MessageType
               ,@IsSystemMessage
               ,@IsRetried
               ,@TimeSent
               ,@ProcessedAt
               ,@EndpointName
               ,@EndpointHostId
               ,@EndpointHost
               ,@CriticalTime
               ,@ProcessingTime
               ,@DeliveryTime
               ,@ConversationId)
END";

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
SELECT * FROM [dbo].[MessagesView] ORDER BY [@SortColumn] @SortDirection OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        public static string CreateMessageViewTable = @"
IF (NOT EXISTS (SELECT * 
                 FROM INFORMATION_SCHEMA.TABLES 
                 WHERE TABLE_SCHEMA = 'dbo' 
                 AND  TABLE_NAME = 'MessagesView'))
BEGIN
    CREATE TABLE [dbo].[MessagesView](
	    [MessageId] [nvarchar](100) NOT NULL,
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
	    [MessageId] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY]
END";

    }
}