﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using KV_reloaded;
using WeifenLuo.WinFormsUI.Docking;

namespace SimpleDota2Editor.Panels
{
    public partial class ObjectsViewPanel : DockContent
    {
        public ObjectsViewPanel()
        {
            undoRedoManager = new UndoRedoManager();
            InitializeComponent();
        }

        public ObjectTypePanel ObjectsType;
        private int lastFreeFolderNum = 0;
        private UndoRedoManager undoRedoManager;

        public void UpdateIcon()
        {
            switch (ObjectsType)
            {
                case ObjectTypePanel.Abilities:
                    this.Icon = new Icon("Resources\\Abilities.ico");
                    break;

                case ObjectTypePanel.AbilitiesOverride:
                    this.Icon = new Icon("Resources\\AbilitiesOverride.ico");
                    break;

                case ObjectTypePanel.Heroes:
                    this.Icon = new Icon("Resources\\Heroes.ico");
                    break;

                case ObjectTypePanel.Units:
                    this.Icon = new Icon("Resources\\Units.ico");
                    break;

                case ObjectTypePanel.Items:
                    this.Icon = new Icon("Resources\\Items.ico");
                    break;
            }
        }

        /// <summary>
        /// Сылка на загруженный файл объектов
        /// </summary>
        private KVToken ObjectKV;

        public void LoadMe(KVToken fileKv)
        {
            treeView1.TreeViewNodeSorter = new NodeSorter();
            ObjectKV = fileKv;
            treeView1.Nodes.Clear();

            int i = 0;
            foreach (var obj in ObjectKV.Children)
            {
                if(obj.Type == KVTokenType.Comment || obj.Type == KVTokenType.KVsimple)
                    continue;

                var kv = obj.SystemComment?.FindKV("Folder");
                if (kv == null)
                    treeView1.Nodes.Add(i.ToString(), obj.Key);
                else
                {
                    string folderPath = kv.Value;
                    int finded = folderPath.IndexOf('\\');
                    TreeNodeCollection lastNodeCollection = treeView1.Nodes;
                    TreeNode node;
                    while (finded != -1)
                    {
                        string folder = folderPath.Substring(0, finded);
                        node = lastNodeCollection.FindNode(folder);
                        if (node == null)
                        {
                            node = lastNodeCollection.Add("#" + folder, folder);
                        }
                        lastNodeCollection = node.Nodes;
                        folderPath = folderPath.Substring(finded + 1); 

                        finded = folderPath.IndexOf('\\');
                    }

                    node = lastNodeCollection.FindNode(folderPath);
                    if (node == null)
                    {
                        node = lastNodeCollection.Add("#" + folderPath, folderPath);
                    }
                    node.Nodes.Add(i.ToString(), obj.Key);
                }
                i++;
            }
            treeView1.Sort();
        }

        public void CloseMe()
        {
            ObjectKV = null;
            treeView1.Nodes.Clear();
        }

        private void treeView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (treeView1.SelectedNode == null)
                return;

            if (treeView1.SelectedNode.Name.Contains("#"))
                return;

            var textPanel = AllPanels.FindPanel(treeView1.SelectedNode.Text);
            if (textPanel == null)
            {
                var panel = new TextEditorPanel();
                panel.PanelName = treeView1.SelectedNode.Text;
                panel.SetText(ObjectKV.GetChild(treeView1.SelectedNode.Text).ChilderToString());
                panel.ObjectRef = ObjectKV.GetChild(treeView1.SelectedNode.Text);
                panel.Show(AllPanels.PrimaryDocking, DockState.Document);
            }
            else
            {
                textPanel.Activate();
            }
            
        }

        public enum ObjectTypePanel
        {
            Abilities,
            AbilitiesOverride,
            Heroes,
            Units,
            Items,
        }

        private void ObjectsViewPanel_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
            AllPanels.Form1.SomeObjectWindowHided(ObjectsType);
        }

        #region SubMenu

        /// <summary>
        /// Меню открыто
        /// </summary>
        private void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (treeView1.SelectedNode == null)
            {
                renameToolStripMenuItem.Enabled = false;
                deleteToolStripMenuItem.Enabled = false;
                return;
            }
        }

        /// <summary>
        /// Меню закрыто
        /// </summary>
        private void contextMenuStrip1_Closed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            createObjectToolStripMenuItem.Enabled = true;
            createFolderToolStripMenuItem.Enabled = true;
            renameToolStripMenuItem.Enabled = true;
            deleteToolStripMenuItem.Enabled = true;
        }

        /// <summary>
        /// Создание объекта
        /// </summary>
        private void createObjectToolStripMenuItem_Click(object sender, System.EventArgs e)
        {
            var obj = CreateObjectForm.ShowAndGet(ObjectKV);
            if (obj == null) return;

            undoRedoManager.Execute(new CreateObjectCommand(treeView1, treeView1.SelectedNode, ObjectKV, obj));
            DataBase.Edited = true;
        }

        /// <summary>
        /// Создание папки
        /// </summary>
        private void createFolderToolStripMenuItem_Click(object sender, System.EventArgs e)
        {
            undoRedoManager.Execute(new CreateFolderCommand(treeView1, treeView1.SelectedNode));
            DataBase.Edited = true;
        }

        /// <summary>
        /// Переименовывание объекта/папки
        /// </summary>
        private void renameToolStripMenuItem_Click(object sender, System.EventArgs e)
        {
            treeView1.SelectedNode.BeginEdit();
        }

        /// <summary>
        /// Удаление объекта/папки(вместе с содержимым)
        /// </summary>
        private void deleteToolStripMenuItem_Click(object sender, System.EventArgs e)
        {
            if (treeView1.SelectedNode == null) return;

            if (treeView1.SelectedNode.Name.Contains("#"))
            {
                undoRedoManager.Execute(new DeleteFolderCommand(treeView1, treeView1.SelectedNode, ObjectKV));
            }
            else
            {
                undoRedoManager.Execute(new DeleteObjectCommand(treeView1, treeView1.SelectedNode, ObjectKV));
            }

            DataBase.Edited = true;
        }


        #endregion

        /// <summary>
        /// TreeNode: Текст отредактирован
        /// </summary>
        private void treeView1_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            if (e.CancelEdit) return;
            if (string.IsNullOrEmpty(e.Label))
            {
                e.CancelEdit = (e.Label != null);
                return;
            }

            if (e.Node.Name.Contains("#"))
            {
                undoRedoManager.Execute(new RenameFolderCommand(treeView1, e.Node, ObjectKV, e.Label));
            }
            else
            {
                undoRedoManager.Execute(new RenameObjectCommand(treeView1, e.Node, ObjectKV, e.Label));
            }

            treeView1.Sort();
            DataBase.Edited = true;
        }

        private void treeView1_ItemDrag(object sender, ItemDragEventArgs e)
        {
            DoDragDrop(e.Item, DragDropEffects.Move);
        }

        private void treeView1_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
            treeView1.SelectedNode = (TreeNode)e.Data.GetData("System.Windows.Forms.TreeNode");
        }

        private void treeView1_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("System.Windows.Forms.TreeNode", false))
            {
                var movingNode = (TreeNode)e.Data.GetData("System.Windows.Forms.TreeNode");
                Point pt = ((TreeView)sender).PointToClient(new Point(e.X, e.Y));
                TreeNode destinationNode = ((TreeView)sender).GetNodeAt(pt);

                if (destinationNode != null)
                    if (!destinationNode.Name.Contains("#"))
                    {
                        destinationNode = destinationNode.Parent;
                    }

                if (destinationNode == null)
                {
                    if (treeView1 != movingNode.TreeView)
                        return;

                    var newNode = (TreeNode)movingNode.Clone();
                    treeView1.Nodes.Add(newNode);
                    movingNode.Remove();

                    if (newNode.Name.Contains("#"))
                        newNode.RenameChildsFolders(ObjectKV, newNode.GetNodePath(""));
                    else
                    {
                        var obj = ObjectKV.GetChild(newNode.Text);
                        obj.SystemComment?.DeleteKV("Folder");
                    }
                }
                else if (destinationNode.TreeView == movingNode.TreeView)
                {
                    var newNode = (TreeNode) movingNode.Clone();
                    destinationNode.Nodes.Add(newNode);
                    destinationNode.Expand();
                    movingNode.Remove();

                    destinationNode.RenameChildsFolders(ObjectKV, destinationNode.GetNodePath(""));
                }
                treeView1.Sort();
            }
        }

        private void treeView1_MouseDown(object sender, MouseEventArgs e)
        {
            //treeView1.SelectedNode = treeView1.GetNodeAt(e.X, e.Y);
        }

        private void treeView1_MouseClick(object sender, MouseEventArgs e)
        {
            treeView1.SelectedNode = treeView1.GetNodeAt(e.X, e.Y);
        }

        private void toolStripButtonUndo_Click(object sender, EventArgs e)
        {
            if (undoRedoManager.CanUndo())
            {
                undoRedoManager.Undo();
            }
        }

        private void toolStripButtonRedo_Click(object sender, EventArgs e)
        {
            if (undoRedoManager.CanRedo())
            {
                undoRedoManager.Redo();
            }
        }

        private void toolStripButtonCreateFolder_Click(object sender, EventArgs e)
        {
            createFolderToolStripMenuItem_Click(null, null);
        }

        private void toolStripButtonCreateObject_Click(object sender, EventArgs e)
        {
            createObjectToolStripMenuItem_Click(null, null);
        }

        private void toolStripButtonDelete_Click(object sender, EventArgs e)
        {
            deleteToolStripMenuItem_Click(null, null);
        }



        #region XXXX

        private class UndoRedoManager
        {
            Stack<ICommand> UndoStack { get; set; }
            Stack<ICommand> RedoStack { get; set; }

            public UndoRedoManager()
            {
                UndoStack = new Stack<ICommand>();
                RedoStack = new Stack<ICommand>();
            }

            public void Undo()
            {
                if (UndoStack.Count > 0)
                {
                    //изымаем команду из стека
                    var command = UndoStack.Pop();
                    //отменяем действие команды
                    command.UnExecute();
                    //заносим команду в стек Redo
                    RedoStack.Push(command);
                    //сигнализируем об изменениях
                    //StateChanged(this, EventArgs.Empty);
                }
            }

            public void Redo()
            {
                if (RedoStack.Count > 0)
                {
                    //изымаем команду из стека
                    var command = RedoStack.Pop();
                    //выполняем действие команды
                    command.Execute();
                    //заносим команду в стек Undo
                    UndoStack.Push(command);
                    //сигнализируем об изменениях
                    //StateChanged(this, EventArgs.Empty);
                }
            }

            //выполняем команду
            public void Execute(ICommand command)
            {
                //выполняем команду
                command.Execute();
                //заносим в стек Undo
                UndoStack.Push(command);
                //очищаем стек Redo
                RedoStack.Clear();
                //сигнализируем об изменениях
                //StateChanged(this, EventArgs.Empty);
            }

            public bool CanUndo()
            {
                return UndoStack.Count != 0;
            }

            public bool CanRedo()
            {
                return RedoStack.Count != 0;
            }

            public string GetUndoActionName()
            {
                if (!CanUndo())
                    return null;

                return UndoStack.Peek().Name;
            }

            public string GetRedoActionName()
            {
                if (!CanRedo())
                    return null;

                return RedoStack.Peek().Name;
            }
        }


        private class CreateFolderCommand : ICommand
        {
            public string Name => "Create folder"; //todo move resource
            private readonly TreeView tree;
            private readonly TreeNode selectedNode;

            public CreateFolderCommand(TreeView tree, TreeNode selectedNode)
            {
                this.tree = tree;
                this.selectedNode = selectedNode;
            }

            public void Execute()
            {
                int lastFreeFolderNum = 0; //todo сделать так чтобы идентификационные порядковые номера папок считывались при загрузки

                if (selectedNode != null && selectedNode.Name.Contains("#"))
                {
                    var node = selectedNode.Nodes.Add("#" + lastFreeFolderNum, "New folder " + lastFreeFolderNum);
                    node.Parent?.Expand();
                }
                else
                {
                    tree.Nodes.Add("#" + lastFreeFolderNum, "New folder " + lastFreeFolderNum);
                }
                lastFreeFolderNum++;
                tree.Sort();
            }

            public void UnExecute()
            {
                
            }
        }

        private class CreateObjectCommand : ICommand
        {
            public string Name => "Create object"; //todo move resource
            private readonly TreeView tree;
            private readonly TreeNode selectedNode;
            private readonly KVToken objectKV;
            private readonly KVToken obj;

            public CreateObjectCommand(TreeView tree, TreeNode selectedNode, KVToken objectKV, KVToken obj)
            {
                this.tree = tree;
                this.selectedNode = selectedNode;
                this.objectKV = objectKV;
                this.obj = obj;
            }

            public void Execute()
            {
                if (selectedNode == null || (selectedNode.Parent == null && !selectedNode.Name.Contains("#")))
                {
                    objectKV.Children.Add(obj);
                    tree.Nodes.Add((objectKV.Children.Count - 1).ToString(), obj.Key);
                    DataBase.Edited = true;
                    return;
                }

                TreeNode node = selectedNode.Parent;
                if (selectedNode.Name.Contains("#"))
                    node = selectedNode;

                string path = node.GetNodePath("");
                if(obj.SystemComment == null)
                    obj.SystemComment = new SystemComment();
                obj.SystemComment.AddKV(new KV() { Key = "Folder", Value = path });
                objectKV.Children.Add(obj);
                node.Nodes.Add((objectKV.Children.Count - 1).ToString(), obj.Key);

                tree.Sort();
            }

            public void UnExecute()
            {

            }
        }

        private class RenameFolderCommand : ICommand
        {
            public string Name => "Rename folder"; //todo move resource
            private readonly TreeView tree;
            private readonly TreeNode node;
            private readonly KVToken objectKV;
            private readonly string newText;

            public RenameFolderCommand(TreeView tree, TreeNode node, KVToken objectKV, string newText)
            {
                this.tree = tree;
                this.node = node;
                this.objectKV = objectKV;
                this.newText = newText;
            }

            public void Execute()
            {
                node.Text = newText;
                node.Name = "#" + newText;
                node.RenameChildsFolders(objectKV, node.GetNodePath(""));
            }

            public void UnExecute()
            {

            }
        }

        private class RenameObjectCommand : ICommand
        {
            public string Name => "Rename object"; //todo move resource
            private readonly TreeView tree;
            private readonly TreeNode node;
            private readonly KVToken objectKV;
            private readonly string newText;

            public RenameObjectCommand(TreeView tree, TreeNode node, KVToken objectKV, string newText)
            {
                this.tree = tree;
                this.node = node;
                this.objectKV = objectKV;
                this.newText = newText;
            }

            public void Execute()
            {
                var textPanel = AllPanels.FindPanel(node.Text);
                if (textPanel != null)
                    textPanel.PanelName = newText;
                var obj = objectKV.GetChild(node.Text);
                obj.Key = newText;
                node.Text = newText;
                node.Name = newText;
            }

            public void UnExecute()
            {

            }
        }

        private class DeleteFolderCommand : ICommand
        {
            public string Name => "Delete folder"; //todo move resource
            private readonly TreeView tree;
            private readonly TreeNode selectedNode;
            private readonly KVToken objectKV;

            public DeleteFolderCommand(TreeView tree, TreeNode selectedNode, KVToken objectKV)
            {
                this.tree = tree;
                this.selectedNode = selectedNode;
                this.objectKV = objectKV;
            }

            public void Execute()
            {
                selectedNode.DeleteChilds(objectKV);
                if (selectedNode.Parent == null)
                    tree.Nodes.Remove(selectedNode);
                else
                    selectedNode.Parent.Nodes.Remove(selectedNode);
            }

            public void UnExecute()
            {

            }
        }

        private class DeleteObjectCommand : ICommand
        {
            public string Name => "Delete object"; //todo move resource
            private readonly TreeView tree;
            private readonly TreeNode selectedNode;
            private readonly KVToken objectKV;

            public DeleteObjectCommand(TreeView tree, TreeNode selectedNode, KVToken objectKV)
            {
                this.tree = tree;
                this.selectedNode = selectedNode;
                this.objectKV = objectKV;
            }

            public void Execute()
            {
                objectKV.RemoveChild(selectedNode.Text);
                selectedNode.Parent.Nodes.Remove(selectedNode);
            }

            public void UnExecute()
            {

            }
        }

        private class MoveFolderCommand : ICommand
        {
            public string Name => "Move object"; //todo move resource

            public void Execute()
            {

            }

            public void UnExecute()
            {

            }
        }

        private class MoveObjectCommand : ICommand
        {
            public string Name => "Move object"; //todo move resource

            public void Execute()
            {

            }

            public void UnExecute()
            {

            }
        }

        #endregion





    }
}
