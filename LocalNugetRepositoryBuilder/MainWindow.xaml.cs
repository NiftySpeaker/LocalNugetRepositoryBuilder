using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LocalNugetRepositoryBuilder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Settings _settings;
        //public List<string> FolderPaths { get; set; }
        //public string TargetFolder { get; set; }

        private string _currentFolderPath;
        IsolatedStorageFile myFile; // = IsolatedStorageFile.GetUserStoreForApplication();
        private string sFile = "Settings.txt";

        public MainWindow()
        {
            InitializeComponent();
            myFile = IsolatedStorageFile.GetStore(IsolatedStorageScope.User | IsolatedStorageScope.Assembly | IsolatedStorageScope.Domain, null, null);
            _settings = new Settings();
            LoadSettings();
            tbTargetFolder.Text = _settings.TargetFolder;
            SetFoldersItemSource();
        }

        private void btnAddSourceFolder(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folderDialog = new FolderBrowserDialog();
            folderDialog.SelectedPath = _currentFolderPath != null ? _currentFolderPath : "C:\\";

            DialogResult result = folderDialog.ShowDialog();
            if (result.ToString() == "OK")
            {
                //FolderPath.Text = folderDialog.SelectedPath;
                if (_settings.FolderPaths.All(fp => fp != folderDialog.SelectedPath))
                {
                    _settings.FolderPaths.Add(folderDialog.SelectedPath);
                    _currentFolderPath = System.IO.Path.GetDirectoryName(folderDialog.SelectedPath);
                    SetFoldersItemSource();
                }
            }
        }

        private void btnRemoveSourceFolder(object sender, RoutedEventArgs e)
        {
            if (lbFolderPaths.SelectedIndex > -1)
            {
                _settings.FolderPaths.RemoveAt(lbFolderPaths.SelectedIndex);
                SetFoldersItemSource();
            }
        }

        private void btnSelectTargetFolder(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folderDialog = new FolderBrowserDialog();
            folderDialog.SelectedPath = _currentFolderPath != null ? _currentFolderPath : "C:\\";

            DialogResult result = folderDialog.ShowDialog();
            if (result.ToString() == "OK")
            {
                _settings.TargetFolder = folderDialog.SelectedPath;
                tbTargetFolder.Text = _settings.TargetFolder;
                setCreateButtonStatus();
            }
        }

        private void btnCreate_Click(object sender, RoutedEventArgs e)
        {
            //Save settings to File
            StreamWriter sw = new StreamWriter(new IsolatedStorageFileStream(sFile, FileMode.OpenOrCreate, myFile));
            var query = new XElement("Settings", 
                                    new XElement("TargetFolder", _settings.TargetFolder),
                                    _settings.FolderPaths.Select(fp => new XElement("Folder", fp))
                                  );

            query.Save(sw);
            sw.Close();

            int count = CreateRepository();
            SuccessMessage.Visibility = System.Windows.Visibility.Visible;
            SuccessMessage.Text = string.Format("Repository created at {0}. Number of added packages: {1} !", 
                                                DateTime.Now.ToString(), count);
        }

        private void setCreateButtonStatus()
        {
            btnCreate.IsEnabled = !string.IsNullOrEmpty(_settings.TargetFolder) && _settings.FolderPaths.Count > 0;
        }

        private int CreateRepository()
        {
            //Alle Nuget Filenamen laden aus den Quellverzeichnissen laden
            var sourceNugetFiles = new List<string>();
            foreach (var folderName in _settings.FolderPaths)
            {
                string[] files = Directory.GetFiles(folderName, "*.nupkg", SearchOption.AllDirectories);
                sourceNugetFiles.AddRange(files);
            }

            //Alle Nuget Filenamen im Zielverzeichnis laden
            var targetNugetFiles = new List<string>();
            string[] existingFiles = Directory.GetFiles(_settings.TargetFolder, "*.nupkg", SearchOption.AllDirectories);
            targetNugetFiles.AddRange(existingFiles.Select(ef => System.IO.Path.GetFileName(ef)));

            //Jetzt die bereits bestehenden Packages herausfilter
            var newPackageFiles = sourceNugetFiles
                                    .Where(snf => !targetNugetFiles.Contains(System.IO.Path.GetFileName(snf)))
                                    .ToList();

            //Jetzt die neuen Package-Dateien ins Zielverzeichnis kopieren
            int fileCopyCounter = 0;
            foreach (var filePath in newPackageFiles)
            {
                string fileName = System.IO.Path.GetFileName(filePath);
                string targetFilePath = System.IO.Path.Combine(_settings.TargetFolder, fileName);
                if (!File.Exists(targetFilePath))
                {
                    File.Copy(filePath, targetFilePath);
                    fileCopyCounter++;
                }
            }
            return fileCopyCounter;
        }

        private void SetFoldersItemSource()
        {
            lbFolderPaths.ItemsSource = null;
            lbFolderPaths.ItemsSource = _settings.FolderPaths;
            setCreateButtonStatus();
        }

        private void LoadSettings()
        {
            if (!myFile.FileExists(sFile))
            {
                IsolatedStorageFileStream dataFile = myFile.CreateFile(sFile);
                dataFile.Close();
                return;
            }

            //Reading and loading data
            StreamReader reader = new StreamReader(new IsolatedStorageFileStream(sFile, FileMode.Open, myFile));
            string rawData = reader.ReadToEnd();
            reader.Close();

            //Deserialze XML to Settings Object
            IEnumerable<XElement> entries = XElement.Parse(rawData).Elements();
            _settings.TargetFolder = entries.Single(e => e.Name == "TargetFolder").Value;
            _settings.FolderPaths = entries.Where(e => e.Name == "Folder").Select(x => x.Value).ToList();
        }

        [Serializable()]
        private class Settings
        {
            public List<string> FolderPaths { get; set; }
            public string TargetFolder { get; set; }

            public Settings()
            {
                this.FolderPaths = new List<string>();
            }
        }
    }
}
