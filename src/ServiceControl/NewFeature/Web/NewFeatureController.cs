namespace ServiceControl.NewFeature.Web;

using Microsoft.AspNetCore.Mvc;
using Persistence.NewFeature;

[ApiController]
[Route("api")]
public class NewFeatureController(INewFeatureDataStore newFeatureDataStoreDataStore)
    : ControllerBase
{
    [Route("new")]
    [HttpGet]
    public IActionResult New()
    {
        return Ok(newFeatureDataStoreDataStore.SayHello());
    }
}