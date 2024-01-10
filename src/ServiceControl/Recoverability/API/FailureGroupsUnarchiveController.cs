namespace ServiceControl.Recoverability.API
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;
    using ServiceControl.Persistence.Recoverability;

    [ApiController]
    [Route("api")]
    class FailureGroupsUnarchiveController(IMessageSession bus, IArchiveMessages archiver) : ControllerBase
    {
        [Route("recoverability/groups/{groupId}/errors/unarchive")]
        [HttpPost]
        public async Task<IActionResult> UnarchiveGroupErrors(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                //TODO compare to CreateErrorResponse that as here before
                return BadRequest("missing groupId");
            }

            if (!archiver.IsOperationInProgressFor(groupId, ArchiveType.FailureGroup))
            {
                await archiver.StartUnarchiving(groupId, ArchiveType.FailureGroup);

                await bus.SendLocal<UnarchiveAllInGroup>(m => { m.GroupId = groupId; });
            }

            return Accepted();
        }
    }
}