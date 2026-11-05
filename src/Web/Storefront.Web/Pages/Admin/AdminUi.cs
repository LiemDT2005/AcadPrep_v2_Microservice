namespace Storefront.Web.Pages.Admin;

// Shared look for Huy's admin pages (Notifications, Billing, AI), copied from the existing Admin Dashboard and
// Account Management pages (_IdentityAdminLayout) so every admin screen reads as one system.
public static class AdminUi
{
    public const string Page = "max-w-[1280px] mx-auto flex flex-col gap-lg";
    public const string Header = "flex flex-col gap-md border-b border-outline-variant pb-md lg:flex-row lg:items-end lg:justify-between";
    public const string Eyebrow = "mb-xs text-[12px] font-semibold uppercase tracking-[0.12em] text-primary";
    public const string Title = "text-[32px] font-bold leading-10 text-on-surface";
    public const string Lead = "mt-sm text-[16px] text-on-surface-variant";

    public const string Card = "rounded-xl border border-outline-variant bg-surface-container-lowest shadow-sm";
    public const string CardBody = "p-lg";
    public const string CardTitle = "text-[18px] font-semibold text-on-surface";
    public const string CardHint = "mt-xs text-[14px] text-on-surface-variant";

    public const string Label = "mb-xs block text-[13px] font-semibold text-on-surface-variant";
    public const string Input = "w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-md py-sm text-[14px] focus:border-primary focus:ring-1 focus:ring-primary/20 transition-colors";
    public const string FieldError = "mt-xs block text-[12px] text-error";
    public const string Help = "mt-xs text-[12px] text-on-surface-variant";

    public const string PrimaryButton = "inline-flex items-center justify-center gap-xs rounded-lg bg-primary px-md py-sm text-[14px] font-medium text-on-primary shadow-sm transition-colors hover:bg-surface-tint cursor-pointer disabled:opacity-60";
    public const string SecondaryButton = "inline-flex items-center justify-center gap-xs rounded-lg border border-primary px-md py-sm text-[14px] font-medium text-primary transition-colors hover:bg-primary/5 cursor-pointer";
    public const string DangerButton = "inline-flex items-center justify-center gap-xs rounded-lg border border-error/40 px-sm py-xs text-[13px] font-medium text-error transition-colors hover:bg-error/5 cursor-pointer";

    public const string Table = "w-full text-left";
    public const string HeadRow = "border-b border-outline-variant bg-surface-bright";
    public const string Th = "px-md py-sm text-[12px] font-semibold uppercase tracking-wider text-on-surface-variant whitespace-nowrap";
    public const string Row = "border-b border-outline-variant/50 hover:bg-surface-container-low transition-colors";
    public const string Td = "px-md py-sm text-[14px] text-on-surface";
    public const string TdMuted = "px-md py-sm text-[13px] text-on-surface-variant whitespace-nowrap";

    public const string Success = "flex items-center gap-sm rounded-lg border border-green-200 bg-green-50 px-md py-sm text-[14px] text-green-800";
    public const string Danger = "flex items-center gap-sm rounded-lg border border-red-200 bg-red-50 px-md py-sm text-[14px] text-red-800";
    public const string Empty = "p-lg text-center text-[14px] text-on-surface-variant";

    public const string Kpi = "rounded-2xl border border-outline-variant bg-surface-container-lowest p-lg shadow-sm";
    public const string KpiLabel = "text-[12px] font-semibold uppercase tracking-wider text-on-surface-variant";
    public const string KpiValue = "mt-sm text-[28px] font-bold leading-9";

    // Order status chips with a coloured dot, same pattern as the account status column.
    public static (string Dot, string Text) StatusTone(string status) => status switch
    {
        "Paid" => ("bg-emerald-500", "text-emerald-700"),
        "Pending" => ("bg-amber-500", "text-amber-700"),
        "Failed" => ("bg-red-500", "text-red-700"),
        _ => ("bg-gray-400", "text-gray-600")
    };

    public static string PlanChip(string planCode) => planCode switch
    {
        "PRO_QUARTER" => "bg-primary/10 text-primary",
        "PRO_MONTH" => "bg-indigo-500/10 text-indigo-700",
        _ => "bg-gray-500/10 text-gray-600"
    };
}
