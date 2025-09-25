using System;
using System.Drawing;
using System.Windows.Forms;

namespace LoadPCDtest.Analysis
{
    /// <summary>
    /// 墙面分离参数配置对话框
    /// </summary>
    public class WallSeparationSettings : Form
    {
        private WallSeparationAnalyzer analyzer;
        
        // 控件
        private NumericUpDown numMaxIterations;
        private NumericUpDown numDistanceThreshold;
        private NumericUpDown numMinPointsForPlane;
        private NumericUpDown numMinVerticalAngle;
        private NumericUpDown numWallMergeAngleThreshold;
        private NumericUpDown numWallMergeDistanceThreshold;
        private CheckBox chkEnableBoundaryConstraints;
        private NumericUpDown numBoundaryConstraintStrength;
        private Button btnOk;
        private Button btnCancel;
        private Button btnReset;

        public WallSeparationSettings(WallSeparationAnalyzer wallAnalyzer)
        {
            analyzer = wallAnalyzer;
            InitializeComponents();
            LoadCurrentSettings();
        }

        private void InitializeComponents()
        {
            Text = "墙面分离参数设置";
            Size = new Size(400, 450);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var mainPanel = new TableLayoutPanel()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 10,
                Padding = new Padding(10)
            };

            // RANSAC参数组
            var ransacGroup = new GroupBox()
            {
                Text = "RANSAC参数",
                Size = new Size(360, 120),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var ransacPanel = new TableLayoutPanel()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(5)
            };

            // 最大迭代次数
            ransacPanel.Controls.Add(new Label() { Text = "最大迭代次数:", TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            numMaxIterations = new NumericUpDown()
            {
                Minimum = 100,
                Maximum = 10000,
                Value = 1000,
                Increment = 100,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            ransacPanel.Controls.Add(numMaxIterations, 1, 0);

            // 距离阈值
            ransacPanel.Controls.Add(new Label() { Text = "距离阈值(米):", TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            numDistanceThreshold = new NumericUpDown()
            {
                Minimum = 0.01m,
                Maximum = 1.0m,
                Value = 0.3m,
                Increment = 0.01m,
                DecimalPlaces = 2,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            ransacPanel.Controls.Add(numDistanceThreshold, 1, 1);

            // 最小平面点数
            ransacPanel.Controls.Add(new Label() { Text = "最小平面点数:", TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
            numMinPointsForPlane = new NumericUpDown()
            {
                Minimum = 50,
                Maximum = 1000,
                Value = 100,
                Increment = 10,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            ransacPanel.Controls.Add(numMinPointsForPlane, 1, 2);

            ransacGroup.Controls.Add(ransacPanel);
            
            // 墙面合并参数组
            var mergeGroup = new GroupBox()
            {
                Text = "墙面合并参数",
                Size = new Size(360, 120),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var mergePanel = new TableLayoutPanel()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(5)
            };

            // 最小垂直角度
            mergePanel.Controls.Add(new Label() { Text = "最小垂直角度(度):", TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            numMinVerticalAngle = new NumericUpDown()
            {
                Minimum = 30,
                Maximum = 90,
                Value = 60,
                Increment = 5,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            mergePanel.Controls.Add(numMinVerticalAngle, 1, 0);

            // 墙面合并角度阈值
            mergePanel.Controls.Add(new Label() { Text = "合并角度阈值(度):", TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            numWallMergeAngleThreshold = new NumericUpDown()
            {
                Minimum = 5,
                Maximum = 45,
                Value = 15,
                Increment = 1,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            mergePanel.Controls.Add(numWallMergeAngleThreshold, 1, 1);

            // 墙面合并距离阈值
            mergePanel.Controls.Add(new Label() { Text = "合并距离阈值(米):", TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
            numWallMergeDistanceThreshold = new NumericUpDown()
            {
                Minimum = 0.1m,
                Maximum = 2.0m,
                Value = 0.3m,
                Increment = 0.1m,
                DecimalPlaces = 1,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            mergePanel.Controls.Add(numWallMergeDistanceThreshold, 1, 2);

            mergeGroup.Controls.Add(mergePanel);

            // 边界约束参数组
            var constraintGroup = new GroupBox()
            {
                Text = "边界约束参数",
                Size = new Size(360, 80),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var constraintPanel = new TableLayoutPanel()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(5)
            };

            // 启用边界约束
            constraintPanel.Controls.Add(new Label() { Text = "启用边界约束:", TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            chkEnableBoundaryConstraints = new CheckBox()
            {
                Checked = false,
                Anchor = AnchorStyles.Left
            };
            constraintPanel.Controls.Add(chkEnableBoundaryConstraints, 1, 0);

            // 边界约束强度
            constraintPanel.Controls.Add(new Label() { Text = "约束强度(0-1):", TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            numBoundaryConstraintStrength = new NumericUpDown()
            {
                Minimum = 0.0m,
                Maximum = 1.0m,
                Value = 0.1m,
                Increment = 0.1m,
                DecimalPlaces = 1,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            constraintPanel.Controls.Add(numBoundaryConstraintStrength, 1, 1);

            constraintGroup.Controls.Add(constraintPanel);

            // 按钮面板
            var buttonPanel = new FlowLayoutPanel()
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Bottom,
                Height = 40,
                Padding = new Padding(10, 5, 10, 5)
            };

            btnCancel = new Button()
            {
                Text = "取消",
                Size = new Size(75, 25),
                DialogResult = DialogResult.Cancel
            };
            buttonPanel.Controls.Add(btnCancel);

            btnOk = new Button()
            {
                Text = "确定",
                Size = new Size(75, 25),
                DialogResult = DialogResult.OK
            };
            btnOk.Click += BtnOk_Click;
            buttonPanel.Controls.Add(btnOk);

            btnReset = new Button()
            {
                Text = "重置",
                Size = new Size(75, 25)
            };
            btnReset.Click += BtnReset_Click;
            buttonPanel.Controls.Add(btnReset);

            // 主布局
            var mainLayout = new TableLayoutPanel()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10)
            };

            mainLayout.Controls.Add(ransacGroup, 0, 0);
            mainLayout.Controls.Add(mergeGroup, 0, 1);
            mainLayout.Controls.Add(constraintGroup, 0, 2);

            Controls.Add(mainLayout);
            Controls.Add(buttonPanel);

            CancelButton = btnCancel;
            AcceptButton = btnOk;
        }

        private void LoadCurrentSettings()
        {
            if (analyzer != null)
            {
                numMaxIterations.Value = analyzer.MaxIterations;
                numDistanceThreshold.Value = (decimal)analyzer.DistanceThreshold;
                numMinPointsForPlane.Value = analyzer.MinPointsForPlane;
                numMinVerticalAngle.Value = (decimal)analyzer.MinVerticalAngle;
                numWallMergeAngleThreshold.Value = (decimal)analyzer.WallMergeAngleThreshold;
                numWallMergeDistanceThreshold.Value = (decimal)analyzer.WallMergeDistanceThreshold;
                chkEnableBoundaryConstraints.Checked = analyzer.EnableBoundaryConstraints;
                numBoundaryConstraintStrength.Value = (decimal)analyzer.BoundaryConstraintStrength;
            }
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            try
            {
                // 应用设置到分析器
                analyzer.MaxIterations = (int)numMaxIterations.Value;
                analyzer.DistanceThreshold = (float)numDistanceThreshold.Value;
                analyzer.MinPointsForPlane = (int)numMinPointsForPlane.Value;
                analyzer.MinVerticalAngle = (float)numMinVerticalAngle.Value;
                analyzer.WallMergeAngleThreshold = (float)numWallMergeAngleThreshold.Value;
                analyzer.WallMergeDistanceThreshold = (float)numWallMergeDistanceThreshold.Value;
                analyzer.EnableBoundaryConstraints = chkEnableBoundaryConstraints.Checked;
                analyzer.BoundaryConstraintStrength = (float)numBoundaryConstraintStrength.Value;

                MessageBox.Show("参数设置已保存", "设置完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存参数时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.None; // 阻止对话框关闭
            }
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            // 重置为默认值
            numMaxIterations.Value = 1000;
            numDistanceThreshold.Value = 0.3m;
            numMinPointsForPlane.Value = 100;
            numMinVerticalAngle.Value = 60;
            numWallMergeAngleThreshold.Value = 15;
            numWallMergeDistanceThreshold.Value = 0.3m;
            chkEnableBoundaryConstraints.Checked = false;
            numBoundaryConstraintStrength.Value = 0.1m;

            MessageBox.Show("参数已重置为默认值", "重置完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
