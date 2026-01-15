using LanShare.Networking;
using System.Collections.ObjectModel;
using System.IO;


namespace HeliShare.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject
    {
        // Вся история (скрытая)
        private List<TransferItem> _allTransfers = new();

        // То, что видит UI (отфильтровано)
        public ObservableCollection<TransferItem> VisibleTransfers { get; } = new();

        private string _currentPeerIp;

        public DashboardViewModel()
        {
            var savedHistory = HistoryManager.Load();
            foreach (var item in savedHistory)
            {
                if (item.Status == "Получен" || item.Status == "Завершено") item.Progress = 100;
                _allTransfers.Add(item);
            }
        }

        // Метод для фильтрации истории под конкретного пользователя
        public void FilterHistory(string peerIp)
        {
            _currentPeerIp = peerIp;
            VisibleTransfers.Clear();

            var filtered = _allTransfers
                .Where(t => t.PeerIdentifier == peerIp)
                .ToList();

            foreach (var item in filtered)
                VisibleTransfers.Add(item);
        }

        public void ClearHistoryForPeer(string peerIp)
        {
            // Удаляем из общего списка все записи этого пользователя
            _allTransfers.RemoveAll(t => t.PeerIdentifier == peerIp);

            // Очищаем то, что видит пользователь на экране
            VisibleTransfers.Clear();

            // Сохраняем изменения в файл
            SaveHistory();
        }

        // Удалить вообще всё (если нужно)
        public void ClearAllHistory()
        {
            _allTransfers.Clear();
            VisibleTransfers.Clear();
            SaveHistory();
        }


        public void SaveHistory() 
        {
            // Сохраняем ВЕСЬ внутренний список, а не отфильтрованный UI
            HistoryManager.Save(_allTransfers); 
        }

        public void AddTransfer(TransferItem item)
        {
            // Добавляем в начало общего списка
            _allTransfers.Insert(0, item);

            // Если это текущий открытый пользователь — добавляем и в UI
            if (item.PeerIdentifier == _currentPeerIp)
            {
                VisibleTransfers.Insert(0, item);
            }

            SaveHistory(); // Сохраняем сразу после добавления
        }
    }
}
