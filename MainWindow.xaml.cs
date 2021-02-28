using System;
using System.Collections.Generic;
using System.Windows;
using System.Linq;
using System.Net;
using System.IO;
using System.ComponentModel;
using WK.Libraries.BetterFolderBrowserNS;
using Microsoft.Win32;
using System.Windows.Media;
using System.Data;


namespace LogMon
{
    public partial class MainWindow : Fluent.RibbonWindow
    {
        #region Declaration and Initialisation
        // Background workers used to run called methods on separate threads dedicated to each worker. This keeps the UI responsive whilst the method runs in the background.
        private readonly BackgroundWorker CyclopsSearchWorker = new BackgroundWorker();
        private readonly BackgroundWorker LantekSearchWorker = new BackgroundWorker();
        //Used to store the response of the part search query
        private readonly List<string> QryContent = new List<string>();

        private readonly SolidColorBrush CyclopsEllipseColor = new SolidColorBrush();
        private readonly SolidColorBrush LantekEllipseColor = new SolidColorBrush();

        private readonly List<string> NewKeywordList = new List<string>();

        public void LoadRegistryKeys()
        {
            RegistryKey regKeys = Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\STAT\\Keywords", false);
            KeywordListBox.Items.Clear();

            foreach (String key in regKeys.GetValueNames())
            {
                KeywordListBox.Items.Add(regKeys.GetValue(key));
            }
        }
        #endregion

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                LoadRegistryKeys();
                SetNewKeywordList();
                this.OptionsGrid.Visibility = Visibility.Hidden;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);   
            }

            //Assigns the method to the DoWork event, which occurs when RunWorkerAsync() is called.
            CyclopsSearchWorker.DoWork += CyclopsSearchWorker_DoWork;
            CyclopsSearchWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(CyclopsSearchWorker_Completed);
            //CyclopsSearchWorker.WorkerReportsProgress = true;

            LantekSearchWorker.DoWork += LantekSearchWorker_DoWork;
            LantekSearchWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(LantekSearchWorker_Completed);
            //LantekSearchWorker.WorkerReportsProgress = true;
        }

        #region Bacgkround Workers
        private void CyclopsSearchWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (QueryPartSearch("Cyclops"))
            {
                e.Result = "true";
            }
            else
            {
                e.Result = "false";
            }

        }

        private void LantekSearchWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (QueryPartSearch("Lantek"))
            {
                e.Result = "true"; ;
            }
            else
            {
                e.Result = "false";
            }
        }

        private void CyclopsSearchWorker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result.ToString() == "true")
            {
                CyclopsEllipseColor.Color = Color.FromRgb(0, 255, 0);
            }
            else
            {
                CyclopsEllipseColor.Color = Color.FromRgb(255, 0, 0);
            }
        }

        private void LantekSearchWorker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result.ToString() == "true")
            {
                LantekEllipseColor.Color = Color.FromRgb(0, 255, 0);
            }
            else
            {
                LantekEllipseColor.Color = Color.FromRgb(255, 0, 0);
            }
        }
        #endregion

        #region Processing
        #region Home Tab
        public List<string> GetBaseDirectories()
        {
            List<string> baseDirectories = new List<String>();
            BetterFolderBrowser baseDirectoryBrowser = new BetterFolderBrowser
            {
                Title = "Select Directories",
                Multiselect = true
            };

            if (baseDirectoryBrowser.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {

                foreach (string path in baseDirectoryBrowser.SelectedFolders)
                {
                    baseDirectories.Add(path);
                }
            }
            return baseDirectories;
        }

        public List<string> GetFileContent(string fileName)
        {
            List<string> fileContent = new List<string>();

            using (StreamReader r = new StreamReader(fileName))
            {
                String line;
                while ((line = r.ReadLine()) != null)
                {
                    fileContent.Add(line);
                }
            }
            return fileContent;
        }

        public string AnalyzeFileContent(List<string> fileContent)
        {
            string resultString = "Failure";
            for (int i = 0; i < NewKeywordList.Count; i++)
            {
                foreach (string line in fileContent)
                {
                    if (line.Contains(NewKeywordList[i]))
                    {
                        resultString = "Success";
                        break;
                    }
                }
            }
            return resultString;
        }

        private String SetFontColor(string file)
        {
            string fontColor = "Black";
            if (AnalyzeFileContent(GetFileContent(file)) != "Success")
            {
                fontColor = "Red";
            }
            return fontColor;
        }

        public void GenerateOverView()
        {
            List<LogFile> items = new List<LogFile>();
            foreach (string directory in GetBaseDirectories())
            {
                DirectoryInfo dir = new DirectoryInfo(directory);
                try
                {
                    string latestFile = (directory) + "\\" + (dir.GetFiles().OrderByDescending(f => f.CreationTime).First().ToString());  //FolderName
                    items.Add(new LogFile()
                    {
                        FolderName = new DirectoryInfo(directory).Name,
                        FilePath = latestFile.ToString(),
                        FileDate = File.GetCreationTime(latestFile.ToString()),
                        Result = AnalyzeFileContent(GetFileContent(latestFile)),
                        TextColor = SetFontColor(latestFile)
                    });
                }
                catch
                {
                    MessageBox.Show((new DirectoryInfo(directory).Name) + " does not contain any log files.");
                }
            }
            OverviewListView.ItemsSource = items;
            OverviewListView.Visibility = Visibility;
        }

        public Boolean QueryPartSearch(string Company)
        {

            String url = "";
            List<String> QryStrings = new List<String>();
            if (Company == "Cyclops")
            {
                url = "https://www.cyclops-electronics.com/services/fast-component-search/?partnumber=BAS16";
                QryStrings.Add("To send a \"Request For Quote\" tick the quote box");
                QryStrings.Add("The part you searched for is not currently in our inventory.");
            }
            else if (Company == "Lantek")
            {
                url = "https://www.lantekcorp.com/part-search/?search_part=BAS16";
                QryStrings.Add("Electronic Component Search Results");
                QryStrings.Add("Parts In Stock");
            }

            Boolean success = false;
            Uri uri = new Uri(@url);

            try
            {
                HttpWebRequest HttpRequest = (HttpWebRequest)WebRequest.Create(uri);
                HttpWebResponse HttpResponse = (HttpWebResponse)HttpRequest.GetResponse();
                Stream stream = HttpResponse.GetResponseStream();
                using (StreamReader sr = new StreamReader(stream))
                {
                    String line;
                    for (int i = 0; i < QryStrings.Count; i++)
                    {
                        while ((line = sr.ReadLine()) != null)
                        {
                            QryContent.Add(line);
                            if (line.Contains(QryStrings[i]))
                            {
                                success = true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            return success;
        }
        #endregion

        #region Options Tab
        public void SetNewKeywordList()
        {
            foreach (String item in KeywordListBox.Items)
            {
                NewKeywordList.Add(item);
            }
        }

        private void UpdateRegistryKeys()
        {
            RegistryKey regKeys = Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\STAT\\Keywords", true);
            NewKeywordList.Clear();

            SetNewKeywordList();

            foreach (String key in regKeys.GetValueNames())
            {
                regKeys.DeleteValue(key);
            }


            for (int i = 0; i < NewKeywordList.Count; i++)
            {
                regKeys.SetValue("Keyword" + (i + 1), NewKeywordList[i]);
            }

        }
        #endregion

        #endregion

        #region Event Handling

        #region Home Tab
        private void HomeTabItem_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.SplitTabControl.Visibility = Visibility.Hidden;
            this.OverviewTabControl.Visibility = Visibility.Visible;
            this.OptionsGrid.Visibility = Visibility.Hidden;
        }

        private void OverviewButton_Click(object sender, RoutedEventArgs e)
        {
            SplitTabControl.Visibility = Visibility.Hidden;
            OverviewTabControl.Visibility = Visibility.Visible;
        }

        private void TabbedButton_Click(object sender, RoutedEventArgs e)
        {
            SplitTabControl.Visibility = Visibility.Visible;
            OverviewTabControl.Visibility = Visibility.Hidden;
        }

        private void OpenFilesButton_Click(object sender, RoutedEventArgs e)
        {
            OverviewListView.ItemsSource = null;
            OpenFilesButton.IsEnabled = false;
            GenerateOverView();
            OpenFilesButton.IsEnabled = true;
            OverviewTabControl.Visibility = Visibility.Visible;
        }

        private void CyclopsButton_Click(object sender, RoutedEventArgs e)
        {
            CyclopsEllipseColor.Color = Color.FromRgb(224, 224, 224);
            CyclopsEllipse.Fill = CyclopsEllipseColor;
            try
            {
                CyclopsSearchWorker.RunWorkerAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void LantekButton_Click(object sender, RoutedEventArgs e)
        {
            LantekEllipseColor.Color = Color.FromRgb(224, 224, 224);
            LantekEllipse.Fill = LantekEllipseColor;
            try
            {
                LantekSearchWorker.RunWorkerAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        #endregion

        #region Options Tab
        private void OptionsTabItem_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.SplitTabControl.Visibility = Visibility.Hidden;
            this.OverviewTabControl.Visibility = Visibility.Hidden;
            this.OptionsGrid.Visibility = Visibility.Visible;
        }

        private void AddKeywordButton_Click(object sender, RoutedEventArgs e)
        {
            if (KeywordTextBox.Text == "")
            {
                MessageBox.Show("Enter a keyword to add to the list!", "No keyword", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                KeywordTextBox.Clear();
            }
            else if (KeywordListBox.Items.Contains(KeywordTextBox.Text))
            {
                MessageBox.Show("This keyword is already on the list!", "Duplicate keyword", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                KeywordTextBox.Clear();
            }
            else
            {
                KeywordListBox.Items.Add(KeywordTextBox.Text);
                KeywordTextBox.Clear();
            }
        }

        private void DelKeywordButton_Click(object sender, RoutedEventArgs e)
        {
            for (int i = KeywordListBox.SelectedItems.Count - 1; i >= 0; i--)
            {
                KeywordListBox.Items.Remove(KeywordListBox.SelectedItems[i]);
            }
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateRegistryKeys();
        }
        #endregion

        #endregion

        #region Work in Progress
        /*
         public void GenerateTabGrid()
        {
            Grid DynamicGrid = new Grid()
            {

            };

            RowDefinition gridRow0 = new RowDefinition()
            {
                Height = new GridLength(21)
            };

            RowDefinition gridRow1 = new RowDefinition()
            {
                Height = new GridLength(1, GridUnitType.Star)
            };
            DynamicGrid.RowDefinitions.Add(gridRow0);
            DynamicGrid.RowDefinitions.Add(gridRow1);
        }

        public void GenerateTabs()
        { 
            TabItem tabpage = new TabItem()
            {
                Header = "Test",
                Tag = "directory",
            };

            ComboBox comboBox = new ComboBox()
            {
                Height = 21,
                Width = 200,
            };
            tabpage.Content = "";
           //Tabbed.Items.Add(tabpage);
            
        }
        */
        #endregion


    }
}
