using AutoWuxia.Core;

namespace AutoWuxia;

static class Program
{
    [STAThread]
    static void Main()
    {
        GameLogger.Info("=== 游戏启动 ===");
        GameLogger.Info($"工作目录: {AppDomain.CurrentDomain.BaseDirectory}");

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            GameLogger.Error("UI线程异常", e.Exception);
            MessageBox.Show($"UI线程异常:\n{e.Exception.Message}\n{e.Exception.StackTrace}",
                "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                GameLogger.Error("未处理异常", ex);
                MessageBox.Show($"未处理异常:\n{ex.Message}\n{ex.StackTrace}",
                    "致命错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        ApplicationConfiguration.Initialize();

        // 加载界面缩放倍率(4K 等高分辨率显示器可在设置里调大)
        Forms.WuxiaTheme.Scale = Config.DisplayConfig.Load().ScaleFactor;

        // 初始化音频管理(加载 audio_config.json,首页设置窗体即可试听)
        AutoWuxia.Systems.AudioManager.Instance.Init();

        // 首页 <-> 主界面循环:MainForm 关闭时若 ReturnToStart=true,则重新显示首页。
        while (true)
        {
            // 显示首页,根据玩家选择决定是否进入主界面
            GameLogger.Info("显示首页 StartForm...");
            using var start = new Forms.StartForm();
            Application.Run(start);

            if (start.RestartRequested)
            {
                GameLogger.Info("缩放变更,重新加载首页");
                continue;
            }

            int? sel = start.Selection;
            if (sel == null || sel == -1)
            {
                GameLogger.Info("玩家从首页退出");
                break;
            }

            GameLogger.Info($"创建主窗体... (选择={sel})");

            Forms.MainForm form;
            if (sel.Value == 0)
            {
                // 新游戏:先弹角色创建窗,取消则回首页循环
                GameLogger.Info("新游戏:打开角色创建窗");
                using var cc = new Forms.CharacterCreateForm();
                if (cc.ShowDialog() != DialogResult.OK || cc.Result == null)
                {
                    GameLogger.Info("玩家取消角色创建,返回首页");
                    continue;
                }
                form = new Forms.MainForm(cc.Result);
            }
            else
            {
                form = new Forms.MainForm(sel.Value);
            }

            using (form)
            {
                GameLogger.Info("进入游戏主循环");
                Application.Run(form);

                // 玩家在主界面点了"返回首页":重新显示首页,否则结束进程
                if (!form.ReturnToStart) break;
                GameLogger.Info("返回首页,重新显示 StartForm...");
            }
        }

        GameLogger.Info("=== 游戏退出 ===");
    }
}
