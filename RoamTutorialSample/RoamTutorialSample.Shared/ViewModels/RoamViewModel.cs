using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using System.Collections.Concurrent;
using System.Threading;

namespace RoamTutorialSample.ViewModels
{
    class BindableBase : INotifyPropertyChanged
    {
        protected bool SetProperty<T>(ref T storage, T value,
          [CallerMemberName] String propertyName = null)
        {
            if (object.Equals(storage, value)) return false;

            storage = value;
            this.OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged(
          [CallerMemberName] string propertyName = null)
        {
            var eventHandler = this.PropertyChanged;
            if (eventHandler != null)
            {
                eventHandler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    internal class SettingsBindableBase : BindableBase
    {
        public enum SettingsScope
        {
            Local,
            Roaming
        }

        static SettingsBindableBase()
        {
            globalTrackedObjects = new ConcurrentQueue<WeakReference<SettingsBindableBase>>();
            ApplicationData.Current.DataChanged += OnRoamingSettingsChanged;
        }

        private static void OnRoamingSettingsChanged(ApplicationData sender, object args)
        {
            List<SettingsBindableBase> liveObjects = new List<SettingsBindableBase>();
            WeakReference<SettingsBindableBase> reference;

            while (globalTrackedObjects.TryDequeue(out reference))
            {
                SettingsBindableBase entry;

                if (reference.TryGetTarget(out entry))
                {
                    liveObjects.Add(entry);
                }
            }
            foreach (var entry in liveObjects)
            {
                globalTrackedObjects.Enqueue(new WeakReference<SettingsBindableBase>(entry));
            }
            foreach (var entry in liveObjects)
            {
                entry.NotifyAllRoamedPropertiesChanged();
            }
        }

        private static ApplicationDataContainer ContainerFromName(string settingName,
            out string key, SettingsScope scope, bool create = false)
        {
            string[] pieces = settingName.Split('.');
            key = pieces[pieces.Length - 1];

            ApplicationDataContainer container =
                scope == SettingsScope.Local
                    ? ApplicationData.Current.LocalSettings
                    : ApplicationData.Current.RoamingSettings;

            for (int i = 0; i < pieces.Length - 1; i++)
            {
                if (container.Containers.ContainsKey(pieces[i]))
                {
                    container = container.Containers[pieces[i]];
                }
                else if (create)
                {
                    container =
                        container.CreateContainer(pieces[i], ApplicationDataCreateDisposition.Always);
                }
                else
                {
                    container = null;
                    break;
                }
            }
            return (container);
        }

        protected T GetSettingsProperty<T>(
            string settingName,
            SettingsScope scope = SettingsScope.Local,
            [CallerMemberName] string propertyName = null)
        {
            T t = default(T);
            string key;

            ApplicationDataContainer container = ContainerFromName(settingName, out key, scope);

            if (container != null)
            {
                object o;

                if (container.Values.TryGetValue(key, out o))
                {
                    // might blow up here...  
                    t = (T)o;

                    if (scope == SettingsScope.Roaming)
                    {
                        this.TrackRoamedPropertyAccess(propertyName);
                    }
                }
            }
            return (t);
        }

        protected void SetSettingsProperty<T>(string settingName, T value,
            SettingsScope scope = SettingsScope.Local,
            [CallerMemberName] string propertyName = null)
        {
            string key;

            ApplicationDataContainer container =
                ContainerFromName(settingName, out key, scope, true);

            container.Values[key] = value;

            this.OnPropertyChanged(propertyName);
        }

        private void TrackRoamedPropertyAccess(string propertyName)
        {
            if (this.syncContext == null)
            {
                this.syncContext = SynchronizationContext.Current;
            }
            if (this.trackedRoamedProperties == null)
            {
                this.TrackRoamedObject();
                this.trackedRoamedProperties = new List<string>();
            }
            if (!this.trackedRoamedProperties.Contains(propertyName))
            {
                this.trackedRoamedProperties.Add(propertyName);
            }
        }

        private void TrackRoamedObject()
        {
            globalTrackedObjects.Enqueue(new WeakReference<SettingsBindableBase>(this));
        }

        private void NotifyAllRoamedPropertiesChanged()
        {
            this.syncContext.Post(
                _ =>
                {
                    if (this.trackedRoamedProperties != null)
                    {
                        foreach (var property in this.trackedRoamedProperties)
                        {
                            base.OnPropertyChanged(property);
                        }
                    }
                },
                null);
        }

        private static ConcurrentQueue<WeakReference<SettingsBindableBase>> globalTrackedObjects;
        private List<string> trackedRoamedProperties;
        private SynchronizationContext syncContext;
    }



    internal class RoamViewModel : SettingsBindableBase
    {
        public string SelectedValue
        {
            get
            {
                return (base.GetSettingsProperty<string>(
                    "UsersImage", SettingsScope.Roaming));
            }
            set
            {
                base.SetSettingsProperty<string>(
                    "UsersImage", value, SettingsScope.Roaming);
            }
        }

        private IEnumerable<string> images;

        public IEnumerable<string> Images
        {
            get
            {
                if (this.images == null)
                {
                    GetImagesAsync();
                }
                return (images);
            }
        }

        private async Task GetImagesAsync()
        {
            // might want to cache this list perhaps.  
            var folder = await Package.Current.InstalledLocation.GetFolderAsync("Images");

            var files = await folder.GetFilesAsync();
            this.images = files
                .Where(f => System.IO.Path.GetExtension(f.Path) == @".png")
                .Select(f => string.Format("Images/{0}", f.Name))
                .ToList();

            this.OnPropertyChanged("Images");
        }

    }
}