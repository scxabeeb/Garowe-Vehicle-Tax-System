using VehicleTax.Web.Models;

namespace VehicleTax.Web.Services;

/// <summary>
/// One plain-language status for an RV, combining its RV status and FMIS
/// status, so finance pages show "Ready to send" instead of "ReadyForFmis".
/// </summary>
public enum RvStage { Preparing, Ready, Sent, Failed, Cancelled }

public static class RvStages
{
    public static RvStage Of(RvStatus status, FmisTransferStatus fmis) =>
        status == RvStatus.Cancelled ? RvStage.Cancelled :
        fmis == FmisTransferStatus.Failed ? RvStage.Failed :
        status == RvStatus.Transferred || fmis == FmisTransferStatus.Transferred ? RvStage.Sent :
        status == RvStatus.ReadyForFmis || fmis == FmisTransferStatus.ReadyForFmis ? RvStage.Ready :
        RvStage.Preparing;

    /// <summary>English label (translated in views), icon and colours.</summary>
    public static (string Label, string Icon, string Fg, string Bg) Look(RvStage s) => s switch
    {
        RvStage.Preparing => ("Being prepared", "bi-pencil-square", "#b45309", "#fef3c7"),
        RvStage.Ready => ("Ready to send", "bi-send", "#0b57d0", "#eef4ff"),
        RvStage.Sent => ("Sent to FMIS", "bi-check-circle-fill", "#15803d", "#dcfce7"),
        RvStage.Failed => ("Sending failed", "bi-exclamation-triangle-fill", "#b91c1c", "#fee2e2"),
        _ => ("Cancelled", "bi-x-circle", "#475569", "#f1f5f9"),
    };
}
