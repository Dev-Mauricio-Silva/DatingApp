using API.Extensions;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc.Filters;

namespace API.Helpers;

public class LogUserActivity : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var resultContext = await next();

        if(context.HttpContext.User.Identity?.IsAuthenticated != true) return;

        var userId = resultContext.HttpContext.User.GetUserId();

        var unityOfWork = resultContext.HttpContext.RequestServices.GetRequiredService<IUnityOfWork>();
        var user = await unityOfWork.UserRepository.GetUserByIdAsync(userId);
        if(user == null) return;
        user.LastActive = DateTime.UtcNow;
        await unityOfWork.Complete();
    }
}
