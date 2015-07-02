namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Nancy;
    using NServiceBus;
    using NServiceBus.Features;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Client.Indexes;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.Extensions;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.MessageFailures.Handlers;

    class FailedMessageClassification : Feature
    {
        public override bool IsEnabledByDefault { get { return true; } }

        public FailedMessageClassification()
        {
            Configure.Component<ExceptionTypeAndStackTraceMessageGrouper>(DependencyLifecycle.SingleInstance);
            Configure.Component<ClassifyFailedMessageEnricher>(DependencyLifecycle.SingleInstance);
        }
    }

    interface IFailureClassifier
    {
        string Name { get; }
        string ClassifyFailure(FailureDetails failureDetails);
    }

    class ExceptionTypeAndStackTraceMessageGrouper : IFailureClassifier
    {
        public string Name { get { return "Exception Type and Stack Trace"; } }
        public string ClassifyFailure(FailureDetails failureDetails)
        {
            var exception = failureDetails.Exception;
            if (exception == null || string.IsNullOrWhiteSpace(exception.StackTrace))
                return null;

            var firstStackTraceFrame = StackTraceParser.Parse(exception.StackTrace).FirstOrDefault();
            if (firstStackTraceFrame == null)
                return null;

            return exception.ExceptionType + " was thrown at " + firstStackTraceFrame.ToMethodIdentifier();
        }
    }

    static class StackTraceParser // "stolen" from https://code.google.com/p/elmah/source/browse/src/Elmah.AspNet/StackTraceParser.cs
    {
        static readonly Regex _regex = new Regex(@"
            ^
            \s*
            \w+ \s+ 
            (?<frame>
                (?<type> .+ ) \.
                (?<method> .+? ) \s*
                (?<params>  \( ( \s* \)
                               |        (?<pt> .+?) \s+ (?<pn> .+?) 
                                 (, \s* (?<pt> .+?) \s+ (?<pn> .+?) )* \) ) )
                ( \s+
                    ( # Microsoft .NET stack traces
                    \w+ \s+ 
                    (?<file> [a-z] \: .+? ) 
                    \: \w+ \s+ 
                    (?<line> [0-9]+ ) \p{P}?  
                    | # Mono stack traces
                    \[0x[0-9a-f]+\] \s+ \w+ \s+ 
                    <(?<file> [^>]+ )>
                    :(?<line> [0-9]+ )
                    )
                )?
            )
            \s* 
            $",
            RegexOptions.IgnoreCase
            | RegexOptions.Multiline
            | RegexOptions.ExplicitCapture
            | RegexOptions.CultureInvariant
            | RegexOptions.IgnorePatternWhitespace
            | RegexOptions.Compiled);

        public static StackFrame[] Parse(string text)
        {
            var stackFrames = new List<StackFrame>();

            var matches = _regex.Matches(text);

            for (var i = 0; i < matches.Count; i++)
            {
                var type = matches[i].Groups["type"].Captures[0].Value;
                var method = matches[i].Groups["method"].Captures[0].Value;
                var parameters = matches[i].Groups["params"].Captures[0].Value;
                var file = matches[i].Groups["file"].Captures[0].Value;
                var line = matches[i].Groups["line"].Captures[0].Value;

                stackFrames.Add(new StackFrame
                {
                    Type = type,
                    Method = method,
                    Params = parameters,
                    File = file,
                    Line = line
                });
            }

            return stackFrames.ToArray();
        }

        public class StackFrame
        {
            public string Type { get; set; }
            public string Method { get; set; }
            public string Params { get; set; }
            public string File { get; set; }
            public string Line { get; set; }

            public string ToMethodIdentifier()
            {
                return Type + "." + Method + Params;
            }
        }
    }

    public class FailureGroupsApi : BaseModule
    {
        public FailureGroupsApi()
        {
            Get["/recoverability/groups"] =
                _ => GetAllGroups();

            Get["/recoverability/groups/{groupId}/errors"] =
                parameters => GetGroupErrors(parameters.GroupId);

            Post["/recoverability/groups/{groupId}/errors/archive"] =
                parameters => ArchiveGroupErrors(parameters.GroupId);
        }

        dynamic GetAllGroups()
        {
            using (var session = Store.OpenSession())
            {
                var results = session.Query<FailureGroupView, FailureGroupsViewIndex>()
                    .Where(x => x.Count > 1)
                    .OrderByDescending(x => x.Last)
                    .ToArray();

                return Negotiate.WithModel(results);
            }
        }

        dynamic GetGroupErrors(string groupId)
        {
            using (var session = Store.OpenSession())
            {
                RavenQueryStatistics stats;

                var results = session.Query<FailureGroupMessageView, FailedMessages_ByGroup>()
                    .Where(x => x.FailureGroupId == groupId && x.Status == FailedMessageStatus.Unresolved)
                    .Statistics(out stats)
                    .Paging(Request)
                    .TransformWith<FailedMessageViewTransformer, FailedMessageView>()
                    .ToArray();

                return Negotiate.WithModel(results)
                    .WithPagingLinksAndTotalCount(stats, Request);
            }
        }

        dynamic ArchiveGroupErrors(string groupId)
        {
            if (String.IsNullOrWhiteSpace(groupId))
            {
                return HttpStatusCode.BadRequest;
            }

            Bus.SendLocal<ArchiveAllInGroup>(m => m.GroupId = groupId);

            return HttpStatusCode.Accepted;
        }

        public IBus Bus { get; set; }
    }

    public class FailureGroupView
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Type { get; set; }
        public int Count { get; set; }
        public DateTime First { get; set; }
        public DateTime Last { get; set; }
    }

    public class FailureGroupsViewIndex : AbstractIndexCreationTask<FailedMessage, FailureGroupView>
    {
        public FailureGroupsViewIndex()
        {
            Map = docs => from doc in docs
                          where doc.Status == FailedMessageStatus.Unresolved
                          let latestAttempt = doc.ProcessingAttempts.Last()
                          let firstAttempt = doc.ProcessingAttempts.First()
                          from failureGroup in doc.FailureGroups
                          select new FailureGroupView
                          {
                              Id = failureGroup.Id,
                              Title = failureGroup.Title,
                              Count = 1,
                              First = firstAttempt.FailureDetails.TimeOfFailure,
                              Last = latestAttempt.FailureDetails.TimeOfFailure,
                              Type = failureGroup.Type
                          };

            Reduce = results => from result in results
                                group result by new
                                {
                                    result.Id,
                                    result.Title,
                                    result.Type
                                }
                                    into g
                                    select new FailureGroupView
                                    {
                                        Id = g.Key.Id,
                                        Title = g.Key.Title,
                                        Count = g.Sum(x => x.Count),
                                        First = g.Min(x => x.First),
                                        Last = g.Max(x => x.Last),
                                        Type = g.Key.Type
                                    };
        }
    }

    public class FailureGroupMessageView
    {
        public string FailureGroupId { get; set; }
        public FailedMessageStatus Status { get; set; }
        public string MessageId { get; set; }
    }

    public class FailedMessages_ByGroup : AbstractIndexCreationTask<FailedMessage, FailureGroupMessageView>
    {
        public FailedMessages_ByGroup()
        {
            Map = docs => from doc in docs
                          from failureGroup in doc.FailureGroups
                          select new FailureGroupMessageView
                {
                    MessageId = doc.UniqueMessageId,
                    FailureGroupId = failureGroup.Id,
                    Status = doc.Status
                };
        }
    }

    class ClassifyFailedMessageEnricher : IFailedMessageEnricher
    {
        public IEnumerable<IFailureClassifier> Classifiers { get; set; }

        public void Enrich(FailedMessage message, ImportFailedMessage source)
        {
            foreach (var classifier in Classifiers)
            {
                var classification = classifier.ClassifyFailure(source.FailureDetails);
                if (classification == null)
                    continue;

                var id = DeterministicGuid.MakeId(classifier.Name, classification).ToString();
                if (!message.FailureGroups.Exists(g => g.Id == id))
                {
                    message.FailureGroups.Add(new FailedMessage.FailureGroup
                    {
                        Id = id,
                        Title = classification,
                        Type = classifier.Name
                    });
                }
            }
        }
    }

    public class ArchiveAllInGroup : ICommand
    {
        public string GroupId { get; set; }
    }

    public class FailedMessageGroupArchived : IEvent
    {
        public string GroupId { get; set; }
    }

    public class ArchiveAllInGroupHandler : IHandleMessages<ArchiveAllInGroup>
    {
        public void Handle(ArchiveAllInGroup message)
        {
            var query = Session.Query<FailureGroupMessageView, FailedMessages_ByGroup>()
                .Where(m => m.FailureGroupId == message.GroupId && m.Status == FailedMessageStatus.Unresolved);

            using (var stream = Session.Advanced.Stream(query))
            {
                while (stream.MoveNext())
                {
                    Session.Advanced.DocumentStore.DatabaseCommands.Patch(
                        FailedMessage.MakeDocumentId(stream.Current.Document.MessageId),
                        new[]
                        {
                            new PatchRequest
                            {
                                Type = PatchCommandType.Set,
                                Name = "Status",
                                Value = (int) FailedMessageStatus.Archived,
                                PrevVal = (int) FailedMessageStatus.Unresolved
                            }
                        });
                }
            }

            Bus.Publish<FailedMessageGroupArchived>(m => m.GroupId = message.GroupId);
        }

        public IDocumentSession Session { get; set; }
        public IBus Bus { get; set; }
    }

    // TODO: New Group Detection

    // TODO: Retry Group

}
