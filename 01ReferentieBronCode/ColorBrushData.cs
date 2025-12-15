namespace ModusPractica
{
    // Klasse om kleurinformatie op te slaan
    public class ColorBrushData
    {
        public string? ResourceName { get; set; }

        public ColorBrushData()
        { }

        public ColorBrushData(string resourceName)
        {
            ResourceName = resourceName;
        }
    }
}