using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using FinancialServices.Api.Models;

namespace FinancialServices.Api.Services;

/// <summary>
/// Renders a persisted <see cref="ReconciliationDossier"/> as a self-contained, printable HTML
/// document (spec §A export; PDF fallback is client-side, pkg 10). Pure + deterministic — no LLM. All
/// dynamic values are HTML-encoded (P6 — no injection). The verbatim red-flag <b>Rule</b> text is
/// shown exactly as the deterministic engine produced it (P2 — this is what defeats "you rigged it").
/// Reconciliation / data-quality language only (P4).
/// </summary>
public static class DossierHtmlRenderer
{
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Default;

    public static string Render(ReconciliationDossier dossier)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        var sb = new StringBuilder(4096);

        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append("<title>").Append(E($"Reconciliation dossier — {dossier.IssuerId}")).Append("</title>");
        sb.Append("<style>")
          .Append("body{font-family:system-ui,Segoe UI,Arial,sans-serif;margin:2rem;color:#111;line-height:1.5}")
          .Append("h1{font-size:1.4rem;margin-bottom:.25rem}h2{font-size:1.05rem;margin-top:1.5rem}")
          .Append("table{border-collapse:collapse;width:100%;margin-top:.5rem}")
          .Append("th,td{border:1px solid #ccc;padding:.4rem .6rem;text-align:left;font-size:.9rem}")
          .Append("th{background:#f3f4f6}.muted{color:#555;font-size:.85rem}")
          .Append(".flag-high{border-left:4px solid #b91c1c}.flag-medium{border-left:4px solid #d97706}")
          .Append(".flag-low{border-left:4px solid #6b7280}.flag{padding:.5rem .75rem;margin:.4rem 0;background:#fafafa}")
          .Append(".rule{font-weight:600}.disclaimer{margin-top:2rem;font-size:.8rem;color:#666}")
          .Append("</style></head><body>");

        sb.Append("<h1>Rating-Reconciliation Dossier</h1>");
        sb.Append("<p class=\"muted\">Issuer <strong>").Append(E(dossier.IssuerId)).Append("</strong> · as-of ")
          .Append(E(Iso(dossier.AsOfDate))).Append(" · generated ").Append(E(Iso(dossier.GeneratedAt)))
          .Append(" · dossier id ").Append(E(dossier.Id)).Append("</p>");
        sb.Append("<p class=\"muted\">Consensus: <strong>").Append(E(dossier.ConsensusSummary))
          .Append("</strong> · Confidence: <strong>")
          .Append(E(dossier.ConfidenceScore.ToString("0.00", CultureInfo.InvariantCulture)))
          .Append("</strong></p>");

        // ── Provider verdicts ────────────────────────────────────────────────────────────────────
        sb.Append("<h2>Provider verdicts</h2><table><thead><tr>")
          .Append("<th>Provider</th><th>Letter</th><th>Notch</th><th>Rating action</th><th>Methodology ref</th>")
          .Append("</tr></thead><tbody>");
        foreach (ProviderRating r in dossier.Ratings)
        {
            sb.Append("<tr><td>").Append(E(r.Provider.ToString())).Append("</td><td>").Append(E(r.Letter))
              .Append("</td><td>").Append(r.Notch.ToString(CultureInfo.InvariantCulture))
              .Append("</td><td>").Append(E(Iso(r.InputAsOfDate)))
              .Append("</td><td>").Append(E(r.MethodologyDocId)).Append("</td></tr>");
        }
        sb.Append("</tbody></table>");

        // ── Red flags (verbatim deterministic rule text) ───────────────────────────────────────────
        sb.Append("<h2>Red flags</h2>");
        if (dossier.Flags.Count == 0)
        {
            sb.Append("<p class=\"muted\">No red flags — the providers reconcile.</p>");
        }
        else
        {
            foreach (RedFlag f in dossier.Flags)
            {
                string severityClass = f.Severity switch
                {
                    "high" => "flag-high",
                    "medium" => "flag-medium",
                    _ => "flag-low",
                };
                sb.Append("<div class=\"flag ").Append(severityClass).Append("\">")
                  .Append("<div class=\"rule\">").Append(E(f.Code)).Append(" (").Append(E(f.Severity)).Append(")</div>")
                  .Append("<div>").Append(E(f.Rule)).Append("</div>");
                if (f.EvidenceRefs.Count > 0)
                {
                    sb.Append("<div class=\"muted\">Evidence: ")
                      .Append(E(string.Join(", ", f.EvidenceRefs))).Append("</div>");
                }
                sb.Append("</div>");
            }
        }

        // ── Divergence decomposition ───────────────────────────────────────────────────────────────
        sb.Append("<h2>Divergence decomposition</h2><table><thead><tr>")
          .Append("<th>Pair</th><th>Notch gap</th><th>Attribution (notches)</th>")
          .Append("</tr></thead><tbody>");
        foreach (PairDivergence d in dossier.Divergences)
        {
            string buckets = string.Join("; ", d.Attribution.Select(b =>
                string.Create(CultureInfo.InvariantCulture, $"{b.Bucket} {b.Notches:0.##}")));
            sb.Append("<tr><td>").Append(E($"{d.A} vs {d.B}")).Append("</td><td>")
              .Append(d.NotchGap.ToString(CultureInfo.InvariantCulture))
              .Append("</td><td>").Append(E(buckets)).Append("</td></tr>");
        }
        sb.Append("</tbody></table>");

        sb.Append("<p class=\"disclaimer\">Prism reconciles credit-rating data and explains why providers ")
          .Append("disagree — a data-quality and provenance tool. Illustrative synthetic corpus.</p>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static string E(string value) => Encoder.Encode(value);

    private static string Iso(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
