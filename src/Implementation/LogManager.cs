using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.ExtendedReflection.Reasoning.ArithmeticSolving;
using Microsoft.ExtendedReflection.Interpretation;
using Microsoft.ExtendedReflection.Metadata;
using System.Globalization;
using Microsoft.Pex.Engine;

namespace PexCustomArithmeticSolvers.Implementation
{
    [__DoNotInstrument]
    public class LogManager
    {
        private string logPath = null;
        private string fileName;
        public string FileName
        {
            get { return this.fileName; }
            set { this.fileName = value; }
        }

        private readonly TermManager termManager;
        private readonly IArithmeticSolvingContext context;
        private readonly bool overwrite;

        private StreamWriter sw;

        public LogManager(string logPrefix, IArithmeticSolvingContext context, string logPath)
        {
            this.logPath = logPath;
            this.termManager = context.Host.ExplorationServices.TermManager;
            this.context = context;
            this.overwrite = false;
            MakeUniqueFileName(logPrefix);
        }

        public LogManager(string logPrefix, IArithmeticSolvingContext context)
        {
            this.termManager = context.Host.ExplorationServices.TermManager;
            this.context = context;
            this.overwrite = false;
            MakeUniqueFileName(logPrefix);
        }
        public LogManager(string logPrefix, bool overwrite)
        {
            this.termManager = null;
            this.context = null;
            this.overwrite = overwrite;
            MakeUniqueFileName(logPrefix);
        }
        private void MakeUniqueFileName(string logPrefix)
        {
            if (this.logPath == null || !Directory.Exists(this.logPath))
            {
                DirectoryInfo di = new DirectoryInfo("logs");
                try
                {
                    if (!di.Exists)
                    {
                        di.Create();
                    }
                    this.logPath = di.FullName;
                }
                catch (Exception e)
                {
                    this.context.Host.Log.LogError(PexLogCategories.ArithmeticSolver, "failed to create log directory: {0}", e.ToString());
                    throw new Exception(e.ToString());
                }
            }

            string[] files = Directory.GetFiles(this.logPath, logPrefix + "_Log_*.txt");
            if (files.Length == 0)
            {
                this.fileName = this.logPath + "\\" + logPrefix + "_Log_1.txt";
            }
            else
            {
                this.fileName = this.logPath + "\\" + logPrefix + "_Log_" + Convert.ToString(files.Length + 1) + ".txt";
            }
        }

        private void OpenFile()
        {
            if (File.Exists(this.fileName) && !this.overwrite)
            {
                this.sw = File.AppendText(this.fileName);
            }
            else
            {
                this.sw = File.CreateText(this.fileName);
            }
        }

        private void CloseFile()
        {
            this.sw.Close();
            this.sw = null;
        }

        private bool TryGetTermValueAsString(ref IArithmeticModel model,
            Term currentTerm, Layout layout, out string result)
        {
            Term term = model.GetValue(currentTerm);
            if (layout == Layout.I1)
            {
                byte bVal;
                if (this.termManager.TryGetI1Constant(term, out bVal))
                {
                    result = Convert.ToString(bVal);
                    return true;
                }
            }
            else if (layout == Layout.I2)
            {
                short sVal;
                if (this.termManager.TryGetI2Constant(term, out sVal))
                {
                    result = Convert.ToString(sVal);
                    return true;
                }
            }
            else if (layout == Layout.I4)
            {
                int iVal;
                if (this.termManager.TryGetI4Constant(term, out iVal))
                {
                    result = Convert.ToString(iVal);
                    return true;
                }
            }
            else if (layout == Layout.I8)
            {
                long lVal;
                if (this.termManager.TryGetI8Constant(term, out lVal))
                {
                    result = Convert.ToString(lVal);
                    return true;
                }
            }
            else if (layout == Layout.R4)
            {
                float fVal;
                if (this.termManager.TryGetR4Constant(term, out fVal))
                {
                    result = fVal.ToString("R");
                    return true;
                }
            }
            else if (layout == Layout.R8)
            {
                double dVal;
                if (this.termManager.TryGetR8Constant(term, out dVal))
                {
                    result = dVal.ToString("R");
                    return true;
                }
            }
            else if (layout.StructType.IsDecimalType)
            {
                decimal decValue;
                if (this.termManager.TryGetDecimalConstant(term, out decValue))
                {
                    result = Convert.ToString(decValue);
                    return true;
                }
            }

            result = null;
            return false;
        }

        public void PrintArithmeticModel(ref IArithmeticModel model)
        {
            PrintArithmeticModel(ref model, null, null);
        }

        public void PrintArithmeticModel(ref IArithmeticModel model, String preMsg, String postMsg)
        {
            OpenFile();
            if (preMsg != null)
            {
                this.sw.WriteLine(preMsg);
            }
            string termVal = "";
            foreach (var variable in this.context.Variables)
            {
                if (TryGetTermValueAsString(ref model, variable.Term,
                    this.termManager.GetLayout(variable.Term), out termVal))
                {
                    this.sw.WriteLine("{0} = {1}", variable.Term, termVal);
                }
                else
                {
                    this.sw.WriteLine("{0} = <unknown>", variable.Term);
                }
            }
            if (postMsg != null)
            {
                this.sw.WriteLine(postMsg);
            }
            CloseFile();
        }

        public void LogSuccess(int evals)
        {
            OpenFile();
            this.sw.WriteLine("Success:{0}", evals);
            CloseFile();
        }

        public void LogFailure()
        {
            OpenFile();
            this.sw.WriteLine("Failure!");
            CloseFile();
        }

        public void LogString(string message)
        {
            OpenFile();
            this.sw.WriteLine(message);
            CloseFile();
        }
    }
}
