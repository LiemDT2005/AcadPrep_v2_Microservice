namespace Storefront.Web.Pages;

// Shared look for Huy's learner pages (Billing, Notifications), taken from the existing learner screens
// (Account/Profile, Exams) on _Layout so plans, orders and notifications read as part of the same site.
public static class LearnerUi
{
    public const string Page = "max-w-7xl mx-auto w-full px-margin-mobile md:px-margin-desktop py-md md:py-xl flex flex-col gap-6";
    public const string NarrowPage = "max-w-3xl mx-auto w-full px-margin-mobile md:px-margin-desktop py-md md:py-xl flex flex-col gap-6";
    public const string Eyebrow = "text-sm font-semibold uppercase tracking-[0.14em] text-primary";
    public const string Title = "text-3xl font-bold tracking-tight text-on-surface";
    public const string Lead = "text-sm text-on-surface-variant";
    public const string Back = "inline-flex items-center gap-1 text-sm font-semibold text-primary hover:underline";

    public const string Card = "rounded-xl border border-outline-variant bg-surface-container-lowest p-6 shadow-sm";
    public const string CardFlat = "rounded-xl border border-outline-variant bg-surface-container-lowest shadow-sm";
    public const string SectionTitle = "text-xl font-bold text-on-surface";

    public const string Label = "mb-1.5 block text-sm font-semibold text-on-surface";
    public const string Input = "h-11 w-full rounded-lg border border-outline-variant bg-white px-4 text-sm outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15";
    public const string FieldError = "mt-1 block text-xs text-error";

    public const string PrimaryButton = "inline-flex items-center justify-center gap-2 rounded-lg bg-primary px-5 py-2.5 text-sm font-semibold text-white transition hover:opacity-90 disabled:opacity-60";
    public const string SecondaryButton = "inline-flex items-center justify-center gap-2 rounded-lg border border-outline-variant px-5 py-2.5 text-sm font-semibold text-on-surface-variant transition hover:bg-surface-container";
    public const string DangerButton = "inline-flex items-center justify-center gap-2 rounded-lg border border-red-200 px-4 py-2 text-sm font-semibold text-error transition hover:bg-red-50";

    public const string TableHead = "bg-surface-container-high border-b border-outline-variant text-on-surface-variant text-xs font-semibold uppercase tracking-wide";
    public const string Th = "p-4 whitespace-nowrap";
    public const string Row = "border-b border-outline-variant/60 last:border-0 hover:bg-surface-container-low transition-colors";
    public const string Td = "p-4 text-sm text-on-surface";
    public const string TdMuted = "p-4 text-sm text-on-surface-variant whitespace-nowrap";

    public const string Success = "flex items-center gap-3 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm font-medium text-emerald-800";
    public const string Danger = "flex items-center gap-3 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800";
    public const string Info = "rounded-xl border border-outline-variant bg-surface-container-low px-4 py-3 text-sm text-on-surface-variant";

    public static (string Dot, string Text, string Icon) OrderTone(string status) => status switch
    {
        "Paid" => ("bg-emerald-500", "text-emerald-700", "check_circle"),
        "Pending" => ("bg-amber-500", "text-amber-700", "schedule"),
        "Failed" => ("bg-red-500", "text-red-700", "cancel"),
        _ => ("bg-gray-400", "text-gray-600", "hourglass_disabled")
    };
}
