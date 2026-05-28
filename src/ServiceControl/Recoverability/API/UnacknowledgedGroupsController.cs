namespace ServiceControl.Recoverability.API
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using ServiceControl.Infrastructure.Auth.Rbac;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Recoverability;

    [ApiController]
    [Route("api")]
    public class UnacknowledgedGroupsController(IRetryHistoryDataStore retryStore, IArchiveMessages archiver) : ControllerBase
    {
        [Authorize(Policy = Permissions.RecoverabilityGroupsView)]
        [Route("recoverability/unacknowledgedgroups/{groupId:required:minlength(1)}")]
        [HttpDelete]
        public async Task<IActionResult> AcknowledgeOperation(string groupId)
        {
            if (archiver.IsArchiveInProgressFor(groupId))
            {
                archiver.DismissArchiveOperation(groupId, ArchiveType.FailureGroup);
                return Ok();
            }

            var success = await retryStore.AcknowledgeRetryGroup(groupId);

            if (success)
            {
                return Ok();
            }


            return NotFound();
        }
    }
}