/// <summary>
/// Level Select ekranından Game sahnesine "hangi level seçildi" bilgisini taşıyan
/// basit bir statik köprü. MonoBehaviour DEĞİL - sahne değişse bile hafızada kalır,
/// bu yüzden DontDestroyOnLoad'a bile gerek yok.
///
/// Game sahnesi doğrudan Editor'den açılıp test edilirse (Level Select'ten
/// geçilmeden) SelectedLevel null kalır - GameManager bu durumda kendi
/// Inspector'ındaki "Fallback Level Data" alanını kullanır.
/// </summary>
public static class LevelSession
{
    public static LevelData SelectedLevel;

    /// <summary>
    /// Pause menüsündeki "Level Select" butonuna basılınca true yapılır.
    /// MainMenuController, sahne açılışında bunu kontrol edip MainMenuPanel yerine
    /// direkt LevelSelectPanel'i gösterir.
    /// </summary>
    public static bool OpenLevelSelectDirectly;
}