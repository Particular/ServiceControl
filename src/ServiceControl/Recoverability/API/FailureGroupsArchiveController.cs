namespace ServiceControl.Recoverability.API
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;
    using ServiceControl.Persistence.Recoverability;

    [ApiController]
    [Route("api")]
    public class FailureGroupsArchiveController(IMessageSession bus, IArchiveMessages archiver) : ControllerBase
    {
        [Route("recoverability/groups/{groupId:required:minlength(1)}/errors/archive")]
        [HttpPost]
        public async Task<IActionResult> ArchiveGroupErrors(string groupId)
        {
            if (!archiver.IsOperationInProgressFor(groupId, ArchiveType.FailureGroup))
            {
                await archiver.StartArchiving(groupId, ArchiveType.FailureGroup);

                await bus.SendLocal<ArchiveAllInGroup>(m => { m.GroupId = groupId; });
            }

            return Accepted();
        }
    }
}