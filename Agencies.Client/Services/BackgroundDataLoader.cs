using Agencies.Core.DTO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Agencies.Client.Services
{
    public class BackgroundDataLoader
    {
        private readonly ApiService _apiService;
        private readonly Dispatcher _dispatcher;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isLoading;
        private readonly ConcurrentDictionary<string, object> _cache;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
        private DateTime _lastCacheUpdate = DateTime.MinValue;

        public event EventHandler<DataLoadedEventArgs> DataLoaded;
        public event EventHandler<string> LoadingStatusChanged;
        public event EventHandler<Exception> LoadingError;

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnLoadingStatusChanged(value ? "Загрузка..." : "Готово");
                }
            }
        }

        public BackgroundDataLoader(ApiService apiService, Dispatcher dispatcher)
        {
            _apiService = apiService;
            _dispatcher = dispatcher;
            _cache = new ConcurrentDictionary<string, object>();
        }

        public async Task LoadAllDataAsync(bool forceRefresh = false)
        {
            if (IsLoading) return;

            try
            {
                IsLoading = true;
                _cancellationTokenSource = new CancellationTokenSource();
                var token = _cancellationTokenSource.Token;

                // Используем Task.WhenAll для параллельной загрузки
                var tasks = new List<Task>
                {
                    Task.Run(() => LoadPropertiesAsync(forceRefresh, token)),
                    Task.Run(() => LoadClientsAsync(forceRefresh, token)),
                    Task.Run(() => LoadDealsAsync(forceRefresh, token))
                };

                await Task.WhenAll(tasks);

                // Кешируем время последнего обновления
                _lastCacheUpdate = DateTime.Now;
            }
            catch (OperationCanceledException)
            {
                OnLoadingStatusChanged("Загрузка отменена");
            }
            catch (Exception ex)
            {
                OnLoadingError(ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadPropertiesAsync(bool forceRefresh, CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                OnLoadingStatusChanged("Загрузка объектов недвижимости...");

                List<PropertyDto> properties;

                if (!forceRefresh && TryGetFromCache("properties", out List<PropertyDto> cachedProperties))
                {
                    properties = cachedProperties;
                }
                else
                {
                    properties = await _apiService.GetPropertiesAsync();
                    _cache["properties"] = properties;
                }

                token.ThrowIfCancellationRequested();

                await _dispatcher.InvokeAsync(() =>
                {
                    OnDataLoaded(new DataLoadedEventArgs
                    {
                        DataType = DataType.Properties,
                        Data = properties
                    });
                });
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new DataLoadException("Ошибка загрузки объектов недвижимости", ex);
            }
        }

        private async Task LoadClientsAsync(bool forceRefresh, CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                OnLoadingStatusChanged("Загрузка клиентов...");

                List<ClientDto> clients;

                if (!forceRefresh && TryGetFromCache("clients", out List<ClientDto> cachedClients))
                {
                    clients = cachedClients;
                }
                else
                {
                    clients = await _apiService.GetClientsAsync();
                    _cache["clients"] = clients;
                }

                token.ThrowIfCancellationRequested();

                await _dispatcher.InvokeAsync(() =>
                {
                    OnDataLoaded(new DataLoadedEventArgs
                    {
                        DataType = DataType.Clients,
                        Data = clients
                    });
                });
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new DataLoadException("Ошибка загрузки клиентов", ex);
            }
        }

        private async Task LoadDealsAsync(bool forceRefresh, CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                OnLoadingStatusChanged("Загрузка сделок...");

                List<DealDto> deals;

                if (!forceRefresh && TryGetFromCache("deals", out List<DealDto> cachedDeals))
                {
                    deals = cachedDeals;
                }
                else
                {
                    deals = await _apiService.GetDealsAsync();
                    _cache["deals"] = deals;
                }

                token.ThrowIfCancellationRequested();

                await _dispatcher.InvokeAsync(() =>
                {
                    OnDataLoaded(new DataLoadedEventArgs
                    {
                        DataType = DataType.Deals,
                        Data = deals
                    });
                });
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new DataLoadException("Ошибка загрузки сделок", ex);
            }
        }

        public async Task<object> LoadStatisticsAsync()
        {
            try
            {
                IsLoading = true;
                OnLoadingStatusChanged("Расчет статистики...");

                var statistics = await Task.Run(async () =>
                {
                    return await _apiService.GetDealStatisticsAsync();
                });

                return statistics;
            }
            catch (Exception ex)
            {
                OnLoadingError(ex);
                return null;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void CancelLoading()
        {
            _cancellationTokenSource?.Cancel();
            IsLoading = false;
        }

        public void ClearCache()
        {
            _cache.Clear();
            _lastCacheUpdate = DateTime.MinValue;
        }

        private bool TryGetFromCache<T>(string key, out T value)
        {
            if (_cache.TryGetValue(key, out object cached) && cached is T typedValue)
            {
                // Проверяем, не устарели ли данные в кеше
                if (DateTime.Now - _lastCacheUpdate < _cacheDuration)
                {
                    value = typedValue;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public bool IsCacheValid()
        {
            return DateTime.Now - _lastCacheUpdate < _cacheDuration;
        }

        protected virtual void OnDataLoaded(DataLoadedEventArgs e)
        {
            DataLoaded?.Invoke(this, e);
        }

        protected virtual void OnLoadingStatusChanged(string status)
        {
            LoadingStatusChanged?.Invoke(this, status);
        }

        protected virtual void OnLoadingError(Exception ex)
        {
            LoadingError?.Invoke(this, ex);
        }
    }

    public class DataLoadedEventArgs : EventArgs
    {
        public DataType DataType { get; set; }
        public object Data { get; set; }
    }

    public enum DataType
    {
        Properties,
        Clients,
        Deals,
        Statistics
    }

    public class DataLoadException : Exception
    {
        public DataLoadException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}