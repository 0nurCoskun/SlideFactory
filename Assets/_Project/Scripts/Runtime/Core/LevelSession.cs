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
}
