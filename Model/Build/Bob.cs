﻿//css_ref System.Data.dll;
//css_import ..\CSGeneral\Utility.cs
//css_import ..\CSGeneral\StringManip.cs
//css_import ..\CSGeneral\MathUtility.cs

using System;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Reflection;

class Bob
{

   /// <summary>
   /// This is Bob's main program. It takes a single argument being the name of a child script
   ///    to execute once a new patch has been extracted. It assumes that the current working
   ///    directory is the APSIM directory.
   /// This script provides 3 environment variables to the child script.
   ///    JobID - the ID in the builds database of the job being run.
   ///    PatchFileName - the file name part of the patch .zip file (no path or extension).
   ///    PatchFileNameFull - the full name of the patch .zip file.
   /// </summary>
   static int Main(string[] args)
   {
      int ReturnCode = 0;

      string ConnectionString = "Data Source=www.apsim.info\\SQLEXPRESS;Database=\"APSIM Builds\";Trusted_Connection=False;User ID=sv-login-external;password=P@ssword123";
      SqlConnection Connection = new SqlConnection(ConnectionString);

      try
      {
         if (args.Length != 1)
            throw new Exception("Usage: cscs Model\\Build\\Bob.cs Model\\Build\\BobMain.cs");
      
         Console.WriteLine("Waiting for a patch...");
                 
         do
         {
            Connection.Open();
            int JobID = FindNextJob(Connection);
            Connection.Close();        

            if (JobID != -1)
            {
               string PatchFileName = DBGet("PatchFileName", Connection, JobID).ToString();
               PatchFileName = PatchFileName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
               Console.WriteLine("Running patch: " + PatchFileName);
               
               // The current working directory will be the APSIM root directory - set the environment variable.
               System.Environment.SetEnvironmentVariable("APSIM", Directory.GetCurrentDirectory());
               
               // Open log file.
               string LogDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(PatchFileName), ".."));
               string LogFileName = Path.Combine(LogDirectory, Path.ChangeExtension(Path.GetFileNameWithoutExtension(PatchFileName), ".txt"));
               StreamWriter Log = new StreamWriter(LogFileName); 
               
               // Clean the tree.
               RemoveUnwantedFiles(Directory.GetCurrentDirectory());
               Run("SVN revert", "svn.exe", "revert -R %APSIM%", Log);
               Run("SVN update", "svn.exe", "update %APSIM%", Log);            

               // Set some environment variables.
               System.Environment.SetEnvironmentVariable("JobID", JobID.ToString());
               System.Environment.SetEnvironmentVariable("PatchFileName", PatchFileName);
               System.Environment.SetEnvironmentVariable("PatchFileNameShort", Path.GetFileNameWithoutExtension(PatchFileName));

               // Extract the patch.
               Run("Extracting patch: " + PatchFileName,
                  "C:\\Program Files\\7-Zip\\7z.exe", 
                  "x -y " + PatchFileName, 
                  Log);

               // Run the patch.
               string CSCS = Assembly.GetCallingAssembly().Location;
               Run("Running patch...", CSCS, args[0], Log);
               
               // Close log file.
               Log.Close();

               Console.WriteLine("Waiting for a patch...");
            }
            else
               Thread.Sleep(1 * 60 * 1000); // 1 minutes

         }
         while (true);
      }
      catch (Exception err)
      {
         Console.WriteLine(err.Message);
         ReturnCode = 1;
      }

      Console.WriteLine("Press return to exit");
      Console.ReadLine();
      return ReturnCode;
   }

    /// <summary>
    /// This program removes all SVN unversioned files from a specified directory.
    /// Can optionally do this recursively.
    /// </summary>
    static void RemoveUnwantedFiles(string directory)
    {
		string StdOut = Run("SVN status", "svn.exe", "status --non-interactive --no-ignore");
        string[] StdOutLines = StdOut.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

		// Loop through all lines the SVN process produced.
		foreach (string line in StdOutLines)
		{
			if (line.Length > 8)
			{
				string relativePath = line.Substring(8);
				string path = Path.Combine(directory, relativePath);

				bool DoDelete = false;
				DoDelete = line[0] == '?' || line[0] == 'I';
				if (DoDelete)
				{
					
					if (Directory.Exists(path))
						Directory.Delete(path, true);
					else if (File.Exists(path))
					{
						try
						{
							File.Delete(path);
						}
						catch (Exception)
						{
							// Must be a locked or readonly file - ignore.
						}
					}
				}
			}
		}
    }
   
   
   
      /////////////////////////////////////////////////////////////////////////////////////////////
      /////////////////////////////////////////////////////////////////////////////////////////////
      /////////////////////////////////////////////////////////////////////////////////////////////
  
      /// <summary>
      /// Find the next job to run. Returns the ID.
      /// </summary>
      static int FindNextJob(SqlConnection Connection)
      {
         string prefix = "";
         if (System.Environment.MachineName.ToUpper() != "BOB")
            prefix = System.Environment.MachineName;
         string SQL = "SELECT ID FROM BuildJobs WHERE " + prefix + "Status = 'Queued' ORDER BY ID";

         SqlDataReader Reader = null;
         try
         {
            SqlCommand Command = new SqlCommand(SQL, Connection);
            Reader = ExecuteReader(Command);
            if (Reader.Read())
                return Convert.ToInt32(Reader[0]);
         }
         finally
         {
            if (Reader != null)
               Reader.Close();
         }
         return -1;
      }
    
      /// <summary>
      /// Return the patch file name of the specified job.
      /// </summary>
      static object DBGet(string FieldName, SqlConnection Connection, int JobID)
      {
         string SQL = "SELECT " + FieldName + " FROM BuildJobs WHERE ID = " + JobID.ToString();

         SqlDataReader Reader = null;
         try
         {
            SqlCommand Command = new SqlCommand(SQL, Connection);
            Reader = ExecuteReader(Command);
            if (Reader.Read())
                return Reader[0];
         }
         finally
         {
            if (Reader != null)
               Reader.Close();
         }
         return "";
      }   

   
      /// <summary>
      /// Execute the specified SqlCommand.
      /// </summary>
      static SqlDataReader ExecuteReader(SqlCommand Cmd)
      {
         // There are often network intermittent issues so try 5 times to execute a reader query
         for (int i = 0; i < 5; i++)
         {
             try
             {
                 return Cmd.ExecuteReader();
             }
             catch (Exception)
             {
                 Thread.Sleep(3 * 60 * 1000); // 3 minutes
             }
         }
         throw new Exception("Cannot execute reader query to SQL server: www.apsim.info");
      }    
   
      // Returns StdOut.
      static string Run(string Name, string Executable, string Arguments, StreamWriter Log = null)
      {
         Executable = CSGeneral.Utility.ReplaceEnvironmentVariables(Executable);
         if (!File.Exists(Executable))
         {
            if (Path.DirectorySeparatorChar == '/') 
               Executable = Path.ChangeExtension(Executable, "");   // linux - remove extension
            Executable = CSGeneral.Utility.FindFileOnPath(Executable);
         }
         Arguments = CSGeneral.Utility.ReplaceEnvironmentVariables(Arguments);
         Process P = CSGeneral.Utility.RunProcess(Executable, Arguments, Directory.GetCurrentDirectory());
         string StdOut = CheckProcessExitedProperly(P);
		 if (Log != null)
		 {
			 if (P.ExitCode != 0)
			 {
				Log.WriteLine(Name + " [Fail]");
				Log.WriteLine(CSGeneral.StringManip.IndentText(StdOut, 4));
			 }
			 else
			 {
				Log.WriteLine(Name + " [Pass]");
				Log.WriteLine(CSGeneral.StringManip.IndentText(StdOut, 4));
			 }
		}
         return StdOut;
      }
      
      static string CheckProcessExitedProperly(Process PlugInProcess)
      {
         if (!PlugInProcess.StartInfo.UseShellExecute)
         {
             string msg = PlugInProcess.StandardOutput.ReadToEnd();
             PlugInProcess.WaitForExit();
             if (PlugInProcess.ExitCode != 0)
                 msg += PlugInProcess.StandardError.ReadToEnd();
             return msg;
         }
         else
             return "";
      }   
  
   }

   
   