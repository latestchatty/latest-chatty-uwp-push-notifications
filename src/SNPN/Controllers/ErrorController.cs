using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;


namespace SNPN.Controllers;

[ApiController]
public class ErrorController : Controller
{
	readonly ILogger<ErrorController> _logger;

	public ErrorController(ILogger<ErrorController> logger)
	{
		_logger = logger;
	}

	[Route("error/{code}")]
	public IActionResult HandleError(int code)
	{
		var guid = Guid.NewGuid();
		var pathFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
		var error = pathFeature?.Error;
		_logger.LogError(error, "{guid} Exception in path {path} ", guid.ToString(), pathFeature?.Path);
		return new ObjectResult(new { status = "error", exceptionid = guid.ToString() });
	}
}