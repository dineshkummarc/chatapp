using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using Coversant.SoapBox.Core.Presence;
using Coversant.SoapBox.Core;
using Coversant.SoapBox.Base;
using Coversant.SoapBox.Core.Message;
using ComponentFactory.Krypton;
using System.Media;
using Coversant.SoapBox.Core.IQ.vCard;
using System.IO;

namespace ChatApp
{
    public partial class MainWindow : ComponentFactory.Krypton.Toolkit.KryptonForm
    {
        private readonly int GroupImageIndex = 6;
        public bool fromcontextmenu = false;
        

        public MainWindow()
        {
            InitializeComponent();

            AppController.Instance.IncomingMessage += new AppController.IncomingMessageDelegate(OnIncomingMessage);
            AppController.Instance.IncomingPresence += new AppController.IncomingPresenceDelegate(OnIncomingPresence);
        }

        public void UpdateContactList()
        {
            tvContacts.BeginUpdate();
            tvContacts.Nodes.Clear();

            foreach (Contact contact in AppController.Instance.Contacts)
            {
                TreeNode GroupNode = GetGroupNodeFor(contact.GroupName);
                TreeNode newNode = new TreeNode(contact.UserName, (int)contact.UserStatus, (int)contact.UserStatus);
                newNode.Tag = contact.JabberId;
                newNode.ContextMenuStrip = this.contactsContextMenuStrip;
                GroupNode.Nodes.Add(newNode);
            }
            tvContacts.ExpandAll();
            tvContacts.EndUpdate();
        }

        public void UpdateContactList1()
        {
            lvContacts.BeginUpdate();
            lvContacts.Items.Clear();

            foreach (Contact contact in AppController.Instance.Contacts)
            {
                ListViewGroup lvGroup = GetGroupNodeFor1(contact.GroupName);
                ListViewItem newItem = new ListViewItem(contact.UserName, (int)contact.UserStatus, lvGroup);
                newItem.SubItems.Add(new ListViewItem.ListViewSubItem(newItem, contact.UserStatus.ToString()));
                newItem.Tag = contact.JabberId.JabberIDNoResource;
                lvContacts.Items.Add(newItem);
            }
            lvContacts.EndUpdate();
        }

        private ListViewGroup GetGroupNodeFor1(string GroupName)
        {
            ListViewGroup lvGroup = null;
            if (GroupName.Length == 0)
            {
                GroupName = "No Group";
            }
            if (lvContacts.Groups[GroupName] != null)
            {
                lvGroup = lvContacts.Groups[GroupName];
            }
            else
            {
                ListViewGroup NewGroup = new ListViewGroup(GroupName, GroupName);
                lvContacts.Groups.Add(NewGroup);
                lvGroup = NewGroup;
            }
            return lvGroup;
        }

        private TreeNode GetGroupNodeFor(string GroupName)
        {
            TreeNode treeNode = null;
            if (GroupName.Length == 0)
            {
                GroupName = "No Group";
            }
            if (tvContacts.Nodes.ContainsKey(GroupName))
            {
                treeNode = tvContacts.Nodes[GroupName];
            }
            else
            {
                TreeNode GroupNode = new TreeNode(GroupName, GroupImageIndex, GroupImageIndex);
                GroupNode.Name = GroupName;
                tvContacts.Nodes.Add(GroupNode);
                treeNode = GroupNode;
            }
            treeNode.ContextMenuStrip = this.GroupContextMenuStrip;
            return treeNode;
        }

        private void MainWindow_Load(object sender, EventArgs e)
        {
            AppController.Instance.SendAvailableRequest();
            if (AppController.Instance.CurrentUser.UserName.Length >= 0)
            {
                lblWelcome.Text = AppController.Instance.CurrentUser.UserName;
            }
            UpdateContactList();
            UpdateContactList1();

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork +=new DoWorkEventHandler(worker_DoWork);
            worker.RunWorkerAsync();
        }

        void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            foreach (Contact contact in AppController.Instance.Contacts)
            {
                vCardResponse response = (vCardResponse)AppController.Instance.SendPacket(new vCardRequest(contact.JabberId), 5000);
                if (response != null)
                {
                    contact.FormattedName = response.VCard.FormattedName == null ? null : response.VCard.FormattedName;
                    if (response.VCard.Photo != null)
                    {
                        Image avatharImage = Image.FromStream(new MemoryStream(response.VCard.Photo.EncodedImage));
                        if (avatharImage != null)
                        {
                            contact.AvatarImage = avatharImage;
                        }
                    }
                }
            }
        }

        private void MainWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.ApplicationExitCall)
            {
                this.Visible = false;
                e.Cancel = true;
                AppController.Instance.SetHiddenMode(this);
            }
        }

        private void lblStatus_LinkClicked(object sender, EventArgs e)
        {
            Control control = sender as Control;
            if (null == control)
                return;
            Point scrnPoint = new Point(0, control.Size.Height);
            statusContextMenuStrip.Show(control, scrnPoint);
        }

        private void BtnLogout_Click(object sender, EventArgs e)
        {
            if (DialogResult.Yes == MessageBox.Show("Are you sure you want to logout?", "Logout", MessageBoxButtons.YesNo, MessageBoxIcon.Question))
            {
                AppController.Instance.Contacts.Dispose();
                this.Close();
                AppController.Instance.LogOff();
            }
        }

        private void tvContacts_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            JabberID jabberID = (JabberID)e.Node.Tag;
            if (jabberID != null)
            {
                MessagingWindow msgWindow = AppController.Instance.GetMessagingWindow(jabberID);
            msgWindow.MessageThreadID = System.Guid.NewGuid().ToString();
            msgWindow.Show();
                msgWindow.Text = string.Format("From {0} to {1}", AppController.Instance.CurrentUser.UserName, jabberID.UserName); 
        }
        }

        private void OnIncomingPresence(PresencePacket incomingPresencePacket)
        {
            this.Invoke(new Session.PacketReceivedDelegate(IncomingAsycPresenceThreadSafe), new object[] { incomingPresencePacket });
        }

        private void IncomingAsycPresenceThreadSafe(Packet incomingPresencePacket)
        {
            PresencePacket IncomingPresencePacket = incomingPresencePacket as PresencePacket;

            if (IncomingPresencePacket is AvailableRequest || IncomingPresencePacket is UnavailableRequest) 
            {
                AppController.Instance.OPlaySound();
                Notify(incomingPresencePacket);
                UpdateContactList();
            }
        }

        private void OnIncomingMessage(AbstractMessagePacket IncomingMessagePacket)
        {
            //Determine the type of packet.
            //If MessageErrorPacket
            //	 Show the MessageErrorPacket in a MessageBox.
            //If MessagePacket
            //   Grab the message window for this user.
            //Determine the type of packet.
            if (IncomingMessagePacket is MessageErrorPacket)
            {
                MessageErrorPacket msgError = (MessageErrorPacket)IncomingMessagePacket;
                MessageBox.Show(string.Format("The following message error packet was received:\n\nCode: {0}\nText: {1}.", msgError.ErrorCode, msgError.ErrorText), "Message Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
            }
            else if (IncomingMessagePacket is MessagePacket)
            {
                MessagePacket msgPacket = (MessagePacket)IncomingMessagePacket;
                AppController.Instance.chPlaySound();
                this.Invoke(new Session.PacketReceivedDelegate(IncomingAsyncMessage), new object[] { msgPacket });
            }
            else
            {
                // It's some other type, just ignore it.
            }
        }

        private void IncomingAsyncMessage(Packet packet)
        {
            MessagePacket IncomingMessage = packet as MessagePacket;

            // Iterate through the list of jabber ids and check whether it is already added
            MessagingWindow msgWindow = AppController.Instance.GetMessagingWindow(packet.From);
            msgWindow.Show();
            if (string.Empty == msgWindow.MessageThreadID)
            {
                msgWindow.MessageThreadID = IncomingMessage.Thread;
            }

            string msg = IncomingMessage.Body;
            msgWindow.Text = string.Format("From {0} to {1}", packet.To.UserName, packet.From.UserName); 
            if (msgWindow.ContainsFocus == false)
            {
                Notify(msg);
            }
            msgWindow.AddMessageToHistory(IncomingMessage);
        }

        private void AddContactMenuItem_Click(object sender, EventArgs e)
        {
            AddContact AddContactWnd = new AddContact();
            AddContactWnd.ShowDialog(this);
            AddContactWnd.Dispose();
            AddContactWnd = null;
        }

        private void DeleteContactMenuItem_Click(object sender, EventArgs e)
        {
            DelContact DelContactWnd = new DelContact();
            DelContactWnd.ShowDialog(this);
            DelContactWnd.Dispose();
            DelContactWnd = null;
        }

        private void EditGroupMenuItem_Click(object sender, EventArgs e)
        {
            EditGroup EditGroupWnd = new EditGroup();
            EditGroupWnd.ShowDialog(this);
            EditGroupWnd.Dispose();
            EditGroupWnd = null;
        }

        private void EditContactMenuItem_Click(object sender, EventArgs e)
        {
            EditContact EditContactWnd = new EditContact();
            EditContactWnd.ShowDialog(this);
            EditContactWnd.Dispose();
            EditContactWnd = null;
        }

        private void DeleteGroupMenuItem_Click(object sender, EventArgs e)
        {
            DeleteGroup DeleteGroupWnd = new DeleteGroup();
            DeleteGroupWnd.ShowDialog(this);
            DeleteGroupWnd.Dispose();
            DeleteGroupWnd = null;
        }

        /// <summary>
        /// update tree view by group
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void sortByGroupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ContactList m_contacts = AppController.Instance.Contacts;
            Contact contact;
            tvContacts.Nodes.Clear();

            for (int i = 0; i < m_contacts.Count; ++i)
            {
                contact = m_contacts[i];
                string groupName = contact.GroupName;

                TreeNode groupNode = new TreeNode(groupName);

                bool bAddGroup = true;
                foreach (TreeNode node in tvContacts.Nodes)
                {
                    //--------- If the tree already contain this group, do not add it
                    if (node.Text.Equals(groupName, StringComparison.OrdinalIgnoreCase))
                    {
                        groupNode = node;
                        bAddGroup = false;
                        break;
                    }
                }

                groupNode.Nodes.Add(contact.UserName);
                if (bAddGroup)
                {
                    groupNode.ImageIndex = 4;
                    groupNode.SelectedImageIndex = 4;
                    tvContacts.Nodes.Add(groupNode);
                }
            }
            UpdateContactList();
        }

        private void MnuExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }


        /// <summary>
        /// Show the contacts types on the serach box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbSearch_TextChanged(object sender, EventArgs e)
        {
            Searchcontact(tbSearch.Text);
        }

        private void Searchcontact(string s_contact)
        {
            tvContacts.Nodes.Clear();
            if (s_contact == "")
            {
                UpdateContactList();
            }
            else
            {
                foreach (Contact contact in AppController.Instance.Contacts)
                {
                    if (contact.UserName.StartsWith(s_contact))
                    {
                        tvContacts.Nodes.Add(contact.UserName.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Start A Chat
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StartChatMenuItem_Click(object sender, EventArgs e)
        {
            if (null == tvContacts.SelectedNode)
            {
                MessageBox.Show("Select a contact", "Contact not selected", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            JabberID jabberID = (JabberID) tvContacts.SelectedNode.Tag;
            AppController.Instance.GetMessagingWindow(jabberID);
            MessagingWindow.ActiveForm.Text = string.Format("From {0} to {1}", AppController.Instance.CurrentUser.UserName, jabberID.UserName); 

        }

        private void StatusMenu_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = sender as ToolStripMenuItem;
            LoginState state = (LoginState)menuItem.Tag;
            AppController.Instance.SendCurrentPresence(state);

            lblStatus.Values.Image = StatusImageList.Images[(int)state];
            lblStatus.Values.Text = state.ToString();
        }

        private void RenametoolStripMenuItem1_Click(object sender, EventArgs e)
        {
            fromcontextmenu = true;
            EditGroup EditGroupWnd = new EditGroup();
            EditGroupWnd.ShowDialog(this);
            EditGroupWnd.Dispose();
            EditGroupWnd = null;
            fromcontextmenu = false;
        }

        private void DeletetoolStripMenuItem2_Click(object sender, EventArgs e)
        {
            fromcontextmenu = true;
            DeleteGroup DeleteGroupWnd = new DeleteGroup();
            DeleteGroupWnd.ShowDialog(this);
            DeleteGroupWnd.Dispose();
            DeleteGroupWnd = null;
        }

        private void lvContacts_Resize(object sender, EventArgs e)
        {
            this.columnUserName.Width = (int)((float)this.splitContainer.Panel1.ClientSize.Width - 60);
            this.columnStatus.Width = this.lvContacts.ClientSize.Width - (this.columnUserName.Width );
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            about aboutWind = new about();
            aboutWind.ShowDialog(this);
            aboutWind.Dispose();
            aboutWind = null;
        }

        private void preferenceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            preference preWind = new preference();
            preWind.ShowDialog(this);
            preWind.Dispose();
            preWind = null;
        }

        private void Notify(Packet incomingPresencePacket)
        {
            string userName = incomingPresencePacket.From.UserName;

            // If the presence packet is from a user other than the current user
            //  and if Show Notification preference is set, then show notification

            if ( (userName != AppController.Instance.CurrentUser.UserName)
                && (ChatApp.Properties.Settings.Default.FriendOnlineShowNotification == true) )
            {
                string message = null;
                string userStatus = "offline";

                if (incomingPresencePacket is AvailableRequest)
                {
                    AvailableRequest availableReq = WConvert.ToAvailableRequest(incomingPresencePacket);
                    userStatus = availableReq.Status;
                }

                message = string.Format("{0} is now {1}", userName, userStatus.ToString());
                AppController.Instance.HiddenWindow.ShowBalloonToolTip(message);
            }
            
        }

        private void Notify(string msg)
        {
            if (ChatApp.Properties.Settings.Default.IncomingMessageShowNotification == true)
            {
                AppController.Instance.HiddenWindow.ShowBalloonToolTip(msg);
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AppController.Instance.ExitApplication();
        }
    }
}
