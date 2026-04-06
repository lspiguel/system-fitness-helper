using SystemFitnessHelper.Ui;

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);

ServiceConnection serviceConnection = new();
Application.Run(new MainForm(serviceConnection));
