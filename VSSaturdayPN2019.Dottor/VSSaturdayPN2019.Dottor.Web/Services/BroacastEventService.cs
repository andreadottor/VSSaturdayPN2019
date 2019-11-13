using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;

namespace VSSaturdayPN2019.Dottor.Web.Services
{
    public class BroacastEventService
    {
        public event FileSystemEventHandler FileSystemChange;

        protected virtual void OnFileSystemChange(FileSystemEventArgs e)
        {
            if (FileSystemChange != null)
                FileSystemChange(this, e);
        }

        public void NotifyChange(WatcherChangeTypes changeType, string directory, string name) 
            => OnFileSystemChange(new FileSystemEventArgs(changeType, directory, name));

    }


    
}
