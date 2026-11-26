namespace SR2MP.Runtime;

// Source text is also the stable lookup key. No game assemblies are needed to
// validate translations, regional fallbacks or names embedded in menu captions.
internal static class PlaygroundLocalization
{
    internal static readonly string[] Languages = { "en", "pl", "de", "fr", "es", "pt", "ru", "ja", "ko", "zh" };
    private static readonly Dictionary<string, string[]> Messages = Load();
    internal static IEnumerable<string> Keys => Messages.Keys;

    internal static string Translate(string? locale, string source)
    {
        var language = (locale ?? "en").Split(new[] { '-', '_' }, StringSplitOptions.None)[0].ToLowerInvariant();
        var index = Array.IndexOf(Languages, language);
        if (index < 0) index = 0;
        if (Messages.TryGetValue(source, out var values)) return values[index];
        // The world name is user data, and must never be translated or replaced.
        foreach (var entry in Messages)
            if (entry.Key.EndsWith(": ", StringComparison.Ordinal) && source.StartsWith(entry.Key, StringComparison.Ordinal))
                return entry.Value[index] + source.Substring(entry.Key.Length);
        return source;
    }

    private static Dictionary<string, string[]> Load()
    {
        var messages = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var line in Translations.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var values = line.TrimEnd('\r').Split('|');
            if (values.Length != Languages.Length || values.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException("Incomplete Playground translation: " + values[0]);
            messages.Add(values[0], values);
        }
        return messages;
    }

    // Columns: en | pl | de | fr | es | pt | ru | ja | ko | zh (Simplified).
    private const string Translations = @"Play with a friend|Graj ze znajomym|Mit Freunden spielen|Jouer avec un ami|Jugar con un amigo|Jogar com um amigo|Играть с другом|フレンドと遊ぶ|친구와 플레이|与好友游玩
Create a world|Utwórz świat|Welt erstellen|Créer un monde|Crear un mundo|Criar um mundo|Создать мир|ワールドを作成|월드 만들기|创建世界
My worlds|Moje światy|Meine Welten|Mes mondes|Mis mundos|Meus mundos|Мои миры|マイワールド|내 월드|我的世界
Controlled by the host|Ustawia host|Vom Host festgelegt|Contrôlé par l’hôte|Controlado por el anfitrión|Controlado pelo anfitrião|Настраивает хост|ホストが設定します|호스트가 설정합니다|由房主控制
World name|Nazwa świata|Weltname|Nom du monde|Nombre del mundo|Nome do mundo|Название мира|ワールド名|월드 이름|世界名称
Enter a world name|Wpisz nazwę świata|Weltnamen eingeben|Saisir un nom de monde|Escribe un nombre de mundo|Digite um nome de mundo|Введите название мира|ワールド名を入力|월드 이름 입력|输入世界名称
Rename multiplayer world|Zmień nazwę świata multiplayer|Mehrspielerwelt umbenennen|Renommer le monde multijoueur|Renombrar mundo multijugador|Renomear mundo multijogador|Переименовать сетевой мир|マルチプレイのワールド名を変更|멀티플레이 월드 이름 변경|重命名多人世界
Copy a singleplayer world|Skopiuj świat singleplayer|Einzelspielerwelt kopieren|Copier un monde solo|Copiar un mundo individual|Copiar mundo individual|Копировать одиночный мир|ソロのワールドをコピー|싱글플레이 월드 복사|复制单人世界
Join a friend|Dołącz do znajomego|Freund beitreten|Rejoindre un ami|Unirse a un amigo|Entrar com um amigo|Присоединиться к другу|フレンドに参加|친구에게 참가|加入好友
Back|Wróć|Zurück|Retour|Volver|Voltar|Назад|戻る|뒤로|返回
That didn't work. Try again|Nie udało się. Spróbuj ponownie|Das hat nicht geklappt. Erneut versuchen|Échec. Réessayer|No ha funcionado. Reintentar|Não deu certo. Tentar novamente|Не получилось. Попробовать снова|失敗しました。もう一度試す|실패했어요. 다시 시도|操作失败，请重试
Create your first world|Utwórz pierwszy świat|Erste Welt erstellen|Créer ton premier monde|Crear tu primer mundo|Criar seu primeiro mundo|Создать первый мир|最初のワールドを作成|첫 월드 만들기|创建第一个世界
Play: |Graj: |Spielen: |Jouer : |Jugar: |Jogar: |Играть: |プレイ：|플레이: |游玩：
Delete multiplayer world|Usuń świat multiplayer|Mehrspielerwelt löschen|Supprimer le monde multijoueur|Eliminar mundo multijugador|Excluir mundo multijogador|Удалить сетевой мир|マルチプレイのワールドを削除|멀티플레이 월드 삭제|删除多人世界
Cancel|Anuluj|Abbrechen|Annuler|Cancelar|Cancelar|Отмена|キャンセル|취소|取消
Yes, delete: |Tak, usuń: |Ja, löschen: |Oui, supprimer : |Sí, eliminar: |Sim, excluir: |Да, удалить: |削除する：|예, 삭제: |确认删除：
Paste code and join|Wklej kod i dołącz|Code einfügen und beitreten|Coller le code et rejoindre|Pegar código y unirse|Colar código e entrar|Вставить код и подключиться|コードを貼り付けて参加|코드 붙여넣고 참가|粘贴代码并加入
How do I get a code?|Skąd wziąć kod?|Wo bekomme ich einen Code?|Comment obtenir un code ?|¿Cómo consigo un código?|Como conseguir um código?|Где взять код?|コードの入手方法|코드는 어떻게 받나요?|如何获取代码？
Copy: |Skopiuj: |Kopieren: |Copier : |Copiar: |Copiar: |Копировать: |コピー：|복사: |复制：
Copied! Open My worlds|Gotowe! Otwórz Moje światy|Kopiert! Meine Welten öffnen|Copié ! Ouvrir Mes mondes|¡Copiado! Abrir Mis mundos|Copiado! Abrir Meus mundos|Готово! Открыть Мои миры|コピー完了！マイワールドを開く|복사 완료! 내 월드 열기|复制完成！打开我的世界
This world already has a multiplayer copy|Ten świat ma już kopię multiplayer|Diese Welt hat bereits eine Mehrspielerkopie|Ce monde a déjà une copie multijoueur|Este mundo ya tiene una copia multijugador|Este mundo já tem uma cópia multijogador|У этого мира уже есть сетевая копия|このワールドにはマルチプレイ用のコピーがあります|이미 멀티플레이 복사본이 있어요|此世界已有多人副本
Original stays unchanged — go back|Oryginał pozostaje bez zmian — wróć|Original bleibt unverändert – zurück|L’original reste intact — retour|El original no cambia — volver|O original não muda — voltar|Оригинал не изменится — назад|元のワールドは変わりません — 戻る|원본은 그대로 유지돼요 — 뒤로|原世界保持不变 — 返回
No singleplayer saves found|Brak zapisów singleplayer|Keine Einzelspielerspielstände gefunden|Aucune sauvegarde solo trouvée|No hay partidas individuales guardadas|Nenhum jogo individual salvo|Нет одиночных сохранений|ソロのセーブデータがありません|싱글플레이 저장 데이터가 없어요|未找到单人存档
Host: pause → Copy invitation code|Host: pauza → Skopiuj kod zaproszenia|Host: Pause → Einladungscode kopieren|Hôte : pause → Copier le code d’invitation|Anfitrión: pausa → Copiar código de invitación|Anfitrião: pausa → Copiar código de convite|Хост: пауза → Копировать код приглашения|ホスト：ポーズ → 招待コードをコピー|호스트: 일시정지 → 초대 코드 복사|房主：暂停 → 复制邀请码
Copy the received code (Ctrl+C)|Skopiuj otrzymany kod (Ctrl+C)|Erhaltenen Code kopieren (Strg+C)|Copie le code reçu (Ctrl+C)|Copia el código recibido (Ctrl+C)|Copie o código recebido (Ctrl+C)|Скопируй полученный код (Ctrl+C)|受け取ったコードをコピー（Ctrl+C）|받은 코드 복사 (Ctrl+C)|复制收到的代码（Ctrl+C）
Paste code and join (same Wi-Fi/LAN)|Wklej kod i dołącz (ta sama sieć Wi-Fi/LAN)|Code einfügen und beitreten (gleiches WLAN/LAN)|Coller et rejoindre (même Wi-Fi/LAN)|Pegar y unirse (misma red Wi-Fi/LAN)|Colar e entrar (mesma rede Wi-Fi/LAN)|Вставить и войти (общая сеть Wi-Fi/LAN)|貼り付けて参加（同じWi-Fi/LAN）|붙여넣고 참가 (같은 Wi-Fi/LAN)|粘贴并加入（同一 Wi-Fi/局域网）
No valid code — copy it first (Ctrl+C)|Brak poprawnego kodu — skopiuj go (Ctrl+C)|Kein gültiger Code – zuerst kopieren (Strg+C)|Code invalide — copie-le d’abord (Ctrl+C)|Código no válido — cópialo primero (Ctrl+C)|Código inválido — copie primeiro (Ctrl+C)|Нет верного кода — скопируй его (Ctrl+C)|有効なコードがありません — コピー（Ctrl+C）|유효한 코드가 없어요 — 먼저 복사 (Ctrl+C)|无有效代码 — 请先复制（Ctrl+C）
Paste again and join|Wklej ponownie i dołącz|Erneut einfügen und beitreten|Coller à nouveau et rejoindre|Pegar de nuevo y unirse|Colar novamente e entrar|Вставить снова и подключиться|もう一度貼り付けて参加|다시 붙여넣고 참가|重新粘贴并加入
Copy invitation code|Skopiuj kod zaproszenia|Einladungscode kopieren|Copier le code d’invitation|Copiar código de invitación|Copiar código de convite|Копировать код приглашения|招待コードをコピー|초대 코드 복사|复制邀请码
Save and disconnect|Zapisz i rozłącz|Speichern und trennen|Sauvegarder et se déconnecter|Guardar y desconectar|Salvar e desconectar|Сохранить и отключиться|セーブして切断|저장 후 연결 해제|保存并断开连接
Checking for updates...|Sprawdzam aktualizacje...|Suche nach Updates...|Recherche de mises à jour...|Buscando actualizaciones...|Buscando atualizações...|Проверка обновлений...|更新を確認中...|업데이트 확인 중...|正在检查更新……
Downloading update...|Pobieram aktualizację...|Update wird heruntergeladen...|Téléchargement de la mise à jour...|Descargando actualización...|Baixando atualização...|Загрузка обновления...|更新をダウンロード中...|업데이트 다운로드 중...|正在下载更新……
Update after closing the game|Zainstaluj aktualizację po wyjściu|Nach dem Beenden aktualisieren|Mettre à jour après avoir quitté|Actualizar al cerrar el juego|Atualizar ao fechar o jogo|Обновить после выхода из игры|ゲーム終了後に更新|게임 종료 후 업데이트|退出游戏后更新
Update scheduled after exit|Aktualizacja po wyjściu z gry|Update nach dem Beenden geplant|Mise à jour prévue après la fermeture|Actualización programada al salir|Atualização agendada após sair|Обновление запланировано после выхода|終了後の更新を予約済み|종료 후 업데이트 예정|已安排退出后更新
You're up to date|Masz aktualną wersję|Du bist auf dem neuesten Stand|Le jeu est à jour|Tienes la última versión|Você está na versão mais recente|Установлена последняя версия|最新バージョンです|최신 버전이에요|已是最新版本
Update needs a different game version|Aktualizacja wymaga innej wersji gry|Update benötigt eine andere Spielversion|Une autre version du jeu est requise|Se necesita otra versión del juego|Requer outra versão do jogo|Нужна другая версия игры|別のゲームバージョンが必要です|다른 게임 버전이 필요해요|更新需要其他游戏版本
Try checking for updates again|Ponów sprawdzanie aktualizacji|Erneut nach Updates suchen|Réessayer la recherche de mises à jour|Volver a buscar actualizaciones|Buscar atualizações novamente|Повторить проверку обновлений|更新をもう一度確認|업데이트 다시 확인|重新检查更新
Check for updates|Sprawdź aktualizacje|Nach Updates suchen|Rechercher des mises à jour|Buscar actualizaciones|Buscar atualizações|Проверить обновления|更新を確認|업데이트 확인|检查更新
Find games on LAN|Znajdź gry w sieci LAN|LAN-Spiele finden|Trouver des parties en LAN|Buscar partidas en LAN|Buscar jogos na rede local|Найти игры в локальной сети|LAN内のゲームを探す|LAN 게임 찾기|查找局域网游戏
Searching for LAN games...|Szukam gier w sieci LAN...|Suche nach LAN-Spielen...|Recherche de parties en LAN...|Buscando partidas en LAN...|Buscando jogos na rede local...|Поиск игр в локальной сети...|LAN内のゲームを検索中...|LAN 게임 검색 중...|正在查找局域网游戏……
No games found — same Wi-Fi and version?|Brak gier — ta sama sieć Wi-Fi i wersja?|Keine Spiele – gleiches WLAN und gleiche Version?|Aucune partie — même Wi-Fi et même version ?|Sin partidas — ¿misma Wi-Fi y versión?|Nenhum jogo — mesma rede Wi-Fi e versão?|Нет игр — одна сеть Wi-Fi и версия?|見つかりません — 同じWi-Fiとバージョンですか？|게임이 없어요 — 같은 Wi-Fi와 버전인가요?|未找到游戏 — 是否使用同一 Wi-Fi 和版本？
LAN search unavailable — try again|Wyszukiwanie LAN niedostępne — ponów|LAN-Suche nicht verfügbar – erneut versuchen|Recherche LAN indisponible — réessayer|Búsqueda LAN no disponible — reintentar|Busca local indisponível — tentar novamente|Поиск в сети недоступен — повторить|LAN検索ができません — もう一度試す|LAN 검색 불가 — 다시 시도|局域网搜索不可用 — 请重试
Next games|Kolejne gry|Weitere Spiele|Parties suivantes|Más partidas|Próximos jogos|Другие игры|次のゲーム|다음 게임|更多游戏
Refresh LAN games|Odśwież gry w LAN|LAN-Spiele aktualisieren|Actualiser les parties LAN|Actualizar partidas LAN|Atualizar jogos locais|Обновить список игр|LANのゲームを更新|LAN 게임 새로고침|刷新局域网游戏
Join: |Dołącz: |Beitreten: |Rejoindre : |Unirse: |Entrar: |Войти: |参加：|참가: |加入：
Joining...|Dołączanie...|Verbindung wird hergestellt...|Connexion en cours...|Uniéndose...|Entrando...|Подключение...|参加中...|참가 중...|正在加入……
Your name|Twoje imię|Dein Name|Ton nom|Tu nombre|Seu nome|Твоё имя|あなたの名前|내 이름|你的名字
Enter your name|Wpisz swoje imię|Gib deinen Namen ein|Entre ton nom|Escribe tu nombre|Digite seu nome|Введи своё имя|名前を入力|이름을 입력하세요|请输入名字
Continue|Dalej|Weiter|Continuer|Continuar|Continuar|Продолжить|次へ|계속|继续";
}
