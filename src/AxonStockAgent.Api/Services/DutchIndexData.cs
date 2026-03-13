namespace AxonStockAgent.Api.Services;

/// <summary>
/// Hardcoded samenstelling van de Nederlandse beursindexen.
/// Bron: Euronext, per maart 2026.
///
/// AEX: 30 aandelen (uitgebreid van 25 naar 30 in september 2025).
/// AMX: 25 midcap aandelen.
/// AMS Next 20: 20 smallcap aandelen (hernoemd van AScX in september 2025).
///
/// EODHD-notatie: TICKER.AS voor Euronext Amsterdam.
/// Let op: WDP (WDP.BR), Fagron (FAGR.BR) en Air France-KLM (AF.PA) zijn
/// gedual-genoteerd — EODHD retourneert NA voor het .AS-suffix.
/// </summary>
public static class DutchIndexData
{
    public record IndexComponent(string Ticker, string Name, string Sector);

    /// <summary>
    /// AEX Index — 30 grootste bedrijven op Euronext Amsterdam.
    /// Samengesteld na de maart 2026 herziening:
    ///   IN:  SBM Offshore (terug uit AMX)
    ///   UIT: Randstad (naar AMX)
    /// </summary>
    public static readonly IndexComponent[] AEX =
    [
        new("ASML",  "ASML Holding",               "Technology"),
        new("SHELL", "Shell",                       "Energy"),
        new("UNA",   "Unilever",                    "Consumer Staples"),
        new("INGA",  "ING Groep",                   "Financials"),
        new("PRX",   "Prosus",                      "Technology"),
        new("REN",   "RELX",                        "Industrials"),
        new("HEIA",  "Heineken",                    "Consumer Staples"),
        new("AD",    "Ahold Delhaize",              "Consumer Staples"),
        new("MT",    "ArcelorMittal",               "Materials"),
        new("ASM",   "ASM International",           "Technology"),
        new("UMG",   "Universal Music Group",       "Communication Services"),
        new("ADYEN", "Adyen",                       "Financials"),
        new("PHIA",  "Philips",                     "Healthcare"),
        new("ABN",   "ABN AMRO Bank",               "Financials"),
        new("KPN",   "KPN",                         "Communication Services"),
        new("NN",    "NN Group",                    "Financials"),
        new("JDEP",  "JDE Peet's",                 "Consumer Staples"),
        new("WKL",   "Wolters Kluwer",              "Industrials"),
        new("DSFIR", "DSM-Firmenich",               "Materials"),
        new("EXO",   "Exor",                        "Financials"),
        new("BESI",  "BE Semiconductor Industries", "Technology"),
        new("ASRNL", "ASR Nederland",               "Financials"),
        new("CVC",   "CVC Capital Partners",        "Financials"),
        new("AGN",   "Aegon",                       "Financials"),
        new("AKZA",  "AkzoNobel",                   "Materials"),
        new("SBMO",  "SBM Offshore",                "Energy"),
        new("WDP",   "Warehouses De Pauw",          "Real Estate"),    // EODHD: WDP.BR (Belgisch)
        new("INPST", "InPost",                      "Industrials"),
        new("MICC",  "Magnum Ice Cream Company",    "Consumer Staples"),
        new("IMCD",  "IMCD",                        "Materials"),
    ];

    /// <summary>
    /// AMX Index — 25 midcap bedrijven op Euronext Amsterdam.
    /// Samengesteld na de maart 2026 herziening:
    ///   IN:  Randstad (uit AEX)
    ///   UIT: SBM Offshore (naar AEX)
    /// Let op: Air France-KLM (geen .AS EODHD-data) en
    ///         Fagron (FAGR.BR, Belgisch) hebben geen werkende .AS-quote.
    /// </summary>
    public static readonly IndexComponent[] AMX =
    [
        new("AALB",   "Aalberts Industries",        "Industrials"),
        new("AMG",    "AMG Critical Materials",     "Materials"),
        new("APAM",   "Aperam",                     "Materials"),
        new("ARCAD",  "Arcadis",                    "Industrials"),
        new("BAMNB",  "Koninklijke BAM Groep",      "Industrials"),
        new("BFIT",   "Basic-Fit",                  "Consumer Discretionary"),
        new("CRBN",   "Corbion",                    "Materials"),
        new("ECMPA",  "Eurocommercial Properties",  "Real Estate"),
        new("FLOW",   "Flow Traders",               "Financials"),
        new("FUR",    "Fugro",                      "Energy"),
        new("GLPG",   "Galapagos",                  "Healthcare"),
        new("HAL",    "HAL Trust",                  "Financials"),
        new("HAVAS",  "Havas",                      "Communication Services"),
        new("HEIJM",  "Heijmans",                   "Industrials"),
        new("ALLFG",  "Allfunds Group",             "Financials"),
        new("OCI",    "OCI",                        "Materials"),
        new("PHARM",  "Pharming Group",             "Healthcare"),
        new("RAND",   "Randstad",                   "Industrials"),
        new("LIGHT",  "Signify",                    "Industrials"),
        new("TWEKA",  "TKH Group",                  "Technology"),
        new("VLK",    "Van Lanschot Kempen",        "Financials"),
        new("VPK",    "Vopak",                      "Energy"),
        new("CTP",    "CTP",                        "Real Estate"),      // EODHD .AS: NA (Tsjechisch)
        new("FAGR",   "Fagron",                     "Healthcare"),       // EODHD: FAGR.BR (Belgisch)
        new("TKWY",   "Just Eat Takeaway",          "Consumer Discretionary"), // delisting in behandeling
    ];

    /// <summary>
    /// AMS Next 20 Index (voorheen AScX) — 20 smallcap bedrijven.
    /// Hernoemd en ingekrompen van 25 naar 20 leden in september 2025.
    /// </summary>
    public static readonly IndexComponent[] AmsNext20 =
    [
        new("ACOMO",  "Amsterdam Commodities",      "Consumer Staples"),
        new("ALFEN",  "Alfen",                      "Industrials"),
        new("AVTX",   "Avantium",                   "Materials"),
        new("BRNL",   "Brunel International",       "Industrials"),
        new("CMCOM",  "CM.com",                     "Technology"),
        new("ENVI",   "Envipco",                    "Industrials"),
        new("FAST",   "Fastned",                    "Industrials"),
        new("FFARM",  "ForFarmers",                 "Consumer Staples"),
        new("KENDR",  "Kendrion",                   "Industrials"),
        new("NEDAP",  "Nedap",                      "Technology"),
        new("NSI",    "NSI",                        "Real Estate"),
        new("PNL",    "PostNL",                     "Industrials"),
        new("SIFG",   "Sif Holding",                "Industrials"),
        new("SLIGR",  "Sligro Food Group",          "Consumer Staples"),
        new("TOM2",   "TomTom",                     "Technology"),
        new("TRIO",   "Triodos Bank",               "Financials"),
        new("AXS",    "Accsys Technologies",        "Materials"),
        new("WHA",    "Wereldhave",                 "Real Estate"),
        new("VASTN",  "Vastned Retail",             "Real Estate"),   // EODHD .AS: NA (dual-listed)
    ];
}
