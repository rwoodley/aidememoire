using Microsoft.AspNetCore.Mvc;
using Persistence;
using Web.Api.Models;

namespace Web.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PairsController : ControllerBase
{
    private readonly IPairsRepository _pairsRepository;

    public PairsController(IPairsRepository pairsRepository)
    {
        _pairsRepository = pairsRepository;
    }

    [HttpPost]
    public async Task<IActionResult> AddPair([FromBody] AddPairRequest request)
    {
        await _pairsRepository.AddPairAsync(request.BucketName, request.Prompt, request.Response);
        return Ok();
    }
}
