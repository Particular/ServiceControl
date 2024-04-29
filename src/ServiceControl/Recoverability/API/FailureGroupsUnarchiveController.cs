namespace ServiceControl.Recoverability.API
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;
    using ServiceControl.Persistence.Recoverability;

    [ApiController]
    [Route("api")]
    public class FailureGroupsUnarchiveController(IMessageSession bus, IArchiveMessages archiver) : ControllerBase
    {
        [Route("recoverability/groups/{groupId:required:minlength(1)}/errors/unarchive")]
        [HttpPost]
        public async Task<IActionResult> UnarchiveGroupErrors(string groupId)
        {
            if (!archiver.IsOperationInProgressFor(groupId, ArchiveType.FailureGroup))
            {
                await archiver.StartUnarchiving(groupId, ArchiveType.FailureGroup);

                await bus.SendLocal<UnarchiveAllInGroup>(m => { m.GroupId = groupId; });
            }

            return Accepted();
        }
    }
}