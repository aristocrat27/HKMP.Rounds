namespace HKMP.Rounds.Networking
{
    internal enum MapTravelAction : byte
    {
        RequestState = 0,
        SetEnabled = 1,
        CreatePhantomMantis = 2,
        TravelToPhantomMantis = 4,
        TravelToPresetMap = 5,
        DeletePhantomMantis = 6
    }
}
