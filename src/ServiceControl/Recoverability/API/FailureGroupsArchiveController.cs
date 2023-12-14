namespace ServiceControl.Recoverability.API
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;
    using ServiceControl.Persistence.Recoverability;

    [ApiController]
    class FailureGroupsArchiveController(IMessageSession bus, IArchiveMessages archiver) : ControllerBase
    {
        [Route("recoverability/groups/{groupId}/errors/archive")]
        [HttpPost]
        public async Task<IActionResult> ArchiveGroupErrors(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                //TODO compare to CreateErrorResponse that as here before
                return BadRequest("missing groupId");
            }

            if (!archiver.IsOperationInProgressFor(groupId, ArchiveType.FailureGroup))
            {
                await archiver.StartArchiving(groupId, ArchiveType.FailureGroup);

                await bus.SendLocal<ArchiveAllInGroup>(m => { m.GroupId = groupId; });
            }

            return Accepted();
        }
    }
}