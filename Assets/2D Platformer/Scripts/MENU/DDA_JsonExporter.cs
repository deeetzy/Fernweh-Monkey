using UnityEngine;
using System.IO;

public class DDA_BulletproofExporter
{
    public static void ExportEvent(string outcome)
    {
        DDA_Agent dda = Object.FindFirstObjectByType<DDA_Agent>();
        if (dda == null || DDA_DataCollector.Instance == null) return;

        string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
        string folderPath = Path.Combine(desktopPath, "FernwehMonkey_Logs");

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string filePath = Path.Combine(folderPath, "DDA_Master_Log.csv");
        bool fileExists = File.Exists(filePath);

        using (StreamWriter sw = File.AppendText(filePath))
        {
            if (!fileExists)
            {
                sw.WriteLine("DataOra,Jucator,Eveniment,TimpJucat_Secunde,StadiuCurent,BossHP_Procent,VietiMaimuta,Dificultate_Target,Dificultate_Smooth,PanicJumps,NoHitTimer");
            }

            string timeStamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string playerName = PlayerPrefs.GetString("PlayerName", "banana");
            string stageName = dda.boss != null ? dda.boss.currentPhase.ToString() : "Necunoscut";
            float bossHP = dda.boss != null ? (dda.boss.currentHealth / dda.boss.maxHealth) * 100f : 0f;
            float lives = dda.player != null ? dda.player.currentLives : 0f;

            string logLine = $"{timeStamp},{playerName},{outcome},{DDA_DataCollector.Instance.timeSinceStageStart:F1},{stageName},{bossHP:F1},{lives},{dda.targetDifficulty:F2},{dda.smoothDifficulty:F2},{DDA_DataCollector.Instance.panicJumpCount:F1},{dda.noHitTimer:F1}";

            sw.WriteLine(logLine);
        }
    }
}