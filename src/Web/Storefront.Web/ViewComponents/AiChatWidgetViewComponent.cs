using Microsoft.AspNetCore.Mvc;

namespace Storefront.Web.ViewComponents;

// FTOEIC-style floating AI tutor (Huy, 2026-11-05) with a conversation picker and the usage ring/popover.
// Rendered by the layout only for Learner sessions; all data is loaded via /Ai/Chat handlers.
public class AiChatWidgetViewComponent : ViewComponent
{
    public IViewComponentResult Invoke() => View();
}
