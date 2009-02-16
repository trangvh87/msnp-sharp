#region Copyright (c) 2002-2008, Bas Geertsema, Xih Solutions (http://www.xihsolutions.net), Thiago.Sayao, Pang Wu, Ethem Evlice
/*
Copyright (c) 2002-2008, Bas Geertsema, Xih Solutions
(http://www.xihsolutions.net), Thiago.Sayao, Pang Wu, Ethem Evlice.
All rights reserved. http://code.google.com/p/msnp-sharp/

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice,
  this list of conditions and the following disclaimer.
* Redistributions in binary form must reproduce the above copyright notice,
  this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.
* Neither the names of Bas Geertsema or Xih Solutions nor the names of its
  contributors may be used to endorse or promote products derived from this
  software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS 'AS IS'
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF
THE POSSIBILITY OF SUCH DAMAGE. 
*/
#endregion

using System;
using System.Collections;
using System.Diagnostics;

namespace MSNPSharp
{
    using MSNPSharp.Core;
    using MSNPSharp.DataTransfer;

    #region ConversationCreatedEvent

    /// <summary>
    /// Used when a new switchboard session is created.
    /// </summary>
    public class ConversationCreatedEventArgs : EventArgs
    {
        /// <summary>
        /// </summary>
        private Conversation _conversation;

        /// <summary>
        /// The affected conversation
        /// </summary>
        public Conversation Conversation
        {
            get
            {
                return _conversation;
            }
            set
            {
                _conversation = value;
            }
        }

        /// <summary>
        /// </summary>
        private object _initiator;

        /// <summary>
        /// The object that requested the switchboard. Null if the conversation was initiated by a remote client.
        /// </summary>
        public object Initiator
        {
            get
            {
                return _initiator;
            }
            set
            {
                _initiator = value;
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public ConversationCreatedEventArgs(Conversation conversation, object initiator)
        {
            _conversation = conversation;
            _initiator = initiator;
        }
    }

    #endregion

    /// <summary>
    /// Provides an easy interface for the client programmer.
    /// </summary>
    /// <remarks>
    /// Messenger is an important class for the client programmer. It provides an
    /// easy interface to communicate with the network. Messenger is a facade which hides all
    /// lower abstractions like message processors, protocol handlers, etc.
    /// Messenger passes through events from underlying objects. This way the client programmer
    /// can connect eventhandlers just once.
    /// </remarks>
    public class Messenger
    {
        #region Members

        private P2PHandler p2pHandler;
        private NSMessageProcessor nsMessageProcessor;
        private NSMessageHandler nsMessageHandler;
        private ConnectivitySettings connectivitySettings = new ConnectivitySettings();
        private Credentials credentials = new Credentials(MsnProtocol.MSNP18);

        #endregion

        #region .ctor
        /// <summary>
        /// Basic constructor to instantiate a Messenger object.
        /// </summary>
        public Messenger()
        {
            nsMessageProcessor = Factory.CreateNameserverProcessor();
            nsMessageHandler = Factory.CreateNameserverHandler();
            nsMessageHandler.Messenger = this;

            p2pHandler = Factory.CreateP2PHandler();
            p2pHandler.NSMessageHandler = nsMessageHandler;

            #region private events
            nsMessageProcessor.ConnectionClosed += delegate
            {
                if (null != P2PHandler)
                {
                    P2PHandler.ClearMessageSessions();
                }
            };

            nsMessageHandler.SBCreated += delegate(object sender, SBCreatedEventArgs ce)
            {
                //Register the p2phandler to handle all incoming p2p message through this switchboard.
                ce.Switchboard.MessageProcessor.RegisterHandler(P2PHandler);

                // check if the request is remote or on our initiative
                if (ce.Initiator != null && (ce.Initiator == this || ce.Initiator == p2pHandler))
                {
                    return;
                }

                // create a conversation object to handle with the switchboard
                Conversation c = new Conversation(this, ce.Switchboard);

                // fire event to notify client programmer
                OnConversationCreated(c, ce.Initiator);

                if (ce.Switchboard is YIMMessageHandler)
                {
                    //We must ensure that ContactJoined event fired after ConversationCreated.
                    (ce.Switchboard as YIMMessageHandler).ForceJoin(ContactList.GetContact(ce.Account, ClientType.EmailMember));
                }

                return;
            };

            p2pHandler.SessionCreated += delegate(object sender, P2PSessionAffectedEventArgs see)
            {
                // set the correct switchboard to send messages to
                lock (p2pHandler.SwitchboardSessions)
                {
                    foreach (SBMessageHandler sb in p2pHandler.SwitchboardSessions)
                    {
                        if (sb.GetType() == typeof(SBMessageHandler) &&
                            sb.Contacts.ContainsKey(see.Session.RemoteContact))
                        {
                            see.Session.MessageProcessor = sb.MessageProcessor;
                            break;
                        }
                    }
                }

                // Accepts by default owner display images and contact emoticons.
                /*
                msnslpHandler.TransferInvitationReceived += delegate(object sndr, MSNSLPInvitationEventArgs ie)
                {
                    if (ie.TransferProperties.DataType == DataTransferType.DisplayImage)
                    {
                        ie.Accept = true;

                        ie.TransferSession.DataStream = nsMessageHandler.Owner.DisplayImage.OpenStream();
                        ie.TransferSession.AutoCloseStream = false;
                        ie.TransferSession.ClientData = nsMessageHandler.Owner.DisplayImage;
                    }
                    else if (ie.TransferProperties.DataType == DataTransferType.Emoticon)
                    {
                        MSNObject msnObject = new MSNObject();
                        msnObject.ParseContext(ie.TransferProperties.Context);

                        // send an emoticon
                        foreach (Emoticon emoticon in nsMessageHandler.Owner.Emoticons.Values)
                        {
                            if (emoticon.Sha == msnObject.Sha)
                            {
                                ie.Accept = true;
                                ie.TransferSession.AutoCloseStream = true;
                                ie.TransferSession.DataStream = emoticon.OpenStream();
                                ie.TransferSession.ClientData = emoticon;
                            }
                        }
                    }
                    else
                    {
                        // forward the invitation to the client programmer
                        if (TransferInvitationReceived != null)
                            TransferInvitationReceived(sndr, ie);
                    }
                    return;
                };

                */
            };

            p2pHandler.SessionClosed += delegate(object sender, P2PSessionAffectedEventArgs e)
            {
                /*
                MSNSLPHandler handler = GetMSNSLPHandler(e.Session);
                if (handler != null)
                {
                    tsMsnslpHandlers.Remove(handler);
                }
                return;
                 * */
            };

            #endregion
        }

        #endregion

        #region Public

        #region Events
        /// <summary>
        /// Occurs when a new conversation is created. Either by a local or remote invitation.
        /// </summary>
        /// <remarks>
        /// You can check the initiator object in the event arguments to see which party initiated the conversation.
        /// This event is called after the messenger server has created a switchboard handler, so there is
        /// always a valid messageprocessor.
        /// </remarks>
        public event EventHandler<ConversationCreatedEventArgs> ConversationCreated;

        /// <summary>
        /// Occurs when a remote client has send an invitation for a filetransfer session.
        /// </summary>
        public event EventHandler<MSNSLPInvitationEventArgs> TransferInvitationReceived;

        #endregion

        #region Properties

        /// <summary>
        /// The handler that handles all incoming P2P framework messages.
        /// </summary>
        /// <remarks>
        /// The handler is defined at the messenger niveau which implies there is a single
        /// p2p handler instance for every logged in account. All switchboard sessions route their messages
        /// to this p2p handler. This enables the feature to start a p2p session in one switchboard session,
        /// and continue, or close it, in another switchboard session.
        /// </remarks>
        public P2PHandler P2PHandler
        {
            get
            {
                return p2pHandler;
            }
            set
            {
                p2pHandler = value;
            }
        }

        /// <summary>
        /// The message processor that is used to send and receive nameserver messages.
        /// </summary>
        /// <remarks>
        /// This processor is mainly used by the nameserver handler.
        /// </remarks>
        public NSMessageProcessor NameserverProcessor
        {
            get
            {
                return nsMessageProcessor;
            }
        }

        /// <summary>
        /// Specifies the connection capabilities of the local machine.
        /// </summary>
        /// <remarks>
        /// Use this property to set specific connectivity settings like proxy servers and custom messenger servers.
        /// </remarks>
        public ConnectivitySettings ConnectivitySettings
        {
            get
            {
                return connectivitySettings;
            }
            set
            {
                connectivitySettings = value;
            }
        }

        /// <summary>
        /// The credentials which identify the messenger account and the client authentication.
        /// </summary>
        /// <remarks>
        /// This property must be set before logging in the messenger service. <b>Both</b> the account properties and
        /// the client identifier codes must be set. The first, the account, specifies the account which represents the local user,
        /// for example 'account@hotmail.com'. The second, the client codes, specifies how this client will authenticate itself
        /// against the messenger server. See <see cref="Credentials"/> for more information about this.
        /// </remarks>
        public Credentials Credentials
        {
            get
            {
                return credentials;
            }
            set
            {
                credentials = value;
            }
        }


        /// <summary>
        /// The message handler that is used to handle incoming nameserver messages.
        /// </summary>
        public NSMessageHandler Nameserver
        {
            get
            {
                return nsMessageHandler;
            }
        }

        /// <summary>
        /// Returns whether there is a connection with the messenger server.
        /// </summary>
        public bool Connected
        {
            get
            {
                return nsMessageProcessor.Connected;
            }
        }

        /// <summary>
        /// A list of all contacts.
        /// </summary>
        /// <remarks>
        ///	This property is a reference to the ContactList object in the <see cref="Nameserver"/> property. This property is added here for convenient access.
        /// </remarks>
        public ContactList ContactList
        {
            get
            {
                return nsMessageHandler.ContactList;
            }
        }

        /// <summary>
        /// A list of all contactgroups.
        /// </summary>
        /// <remarks>
        ///	This property is a reference to the ContactGroups object in the <see cref="Nameserver"/> property. This property is added here for convenient access.
        /// </remarks>
        public ContactGroupList ContactGroups
        {
            get
            {
                return nsMessageHandler.ContactGroups;
            }
        }

        /// <summary>
        /// Offline message service.
        /// </summary>
        /// <remarks>
        /// This property is a reference to the OIMService object in the <see cref="Nameserver"/> property. This property is added here for convenient access.
        /// </remarks>
        public OIMService OIMService
        {
            get
            {
                return nsMessageHandler.OIMService;
            }
        }

        /// <summary>
        /// Space contactcard service.
        /// </summary>
        /// <remarks>
        /// This property is a reference to the SpaceService object in the <see cref="Nameserver"/> property. This property is added here for convenient access.
        /// </remarks>
        public ContactSpaceService SpaceService
        {
            get
            {
                return nsMessageHandler.SpaceService;
            }
        }

        /// <summary>
        /// Storage service to get/update display name, personal status, display picture etc.
        /// </summary>
        /// <remarks>
        /// This property is a reference to the StorageService object in the <see cref="Nameserver"/> property. This property is added here for convenient access.
        /// </remarks>
        public MSNStorageService StorageService
        {
            get
            {
                return nsMessageHandler.StorageService;
            }
        }

        public WhatsUpService WhatsUpService
        {
            get
            {
                return nsMessageHandler.WhatsUpService;
            }
        }

        /// <summary>
        /// Contact service.
        /// </summary>
        /// <remarks>
        /// This property is a reference to the ContactService object in the <see cref="Nameserver"/> property. This property is added here for convenient access.
        /// </remarks>
        public ContactService ContactService
        {
            get
            {
                return nsMessageHandler.ContactService;
            }
        }

        /// <summary>
        /// The local user logged into the network.
        /// </summary>
        /// <remarks>
        /// This property is a reference to the Owner object in the <see cref="Nameserver"/> property. This property is added here for convenient access.
        /// </remarks>
        public Owner Owner
        {
            get
            {
                return nsMessageHandler.Owner;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Connect to the messenger network.
        /// </summary>
        public virtual void Connect()
        {
            if (nsMessageProcessor == null)
                throw new MSNPSharpException("No message processor defined");

            if (nsMessageHandler == null)
                throw new MSNPSharpException("No message handler defined");

            if (credentials == null)
                throw new MSNPSharpException("No credentials defined");

            if (credentials.Account.Length == 0)
                throw new MSNPSharpException("The specified account is empty");

            if (credentials.Password.Length == 0)
                throw new MSNPSharpException("The specified password is empty");

            if (credentials.ClientCode.Length == 0 || credentials.ClientID.Length == 0)
                throw new MSNPSharpException("The local messengerclient credentials (client-id and client code) are not specified. This is necessary in order to authenticate the local client with the messenger server. See for more info about the values to use the documentation of the Credentials class.");

            // everything is okay, resume
            nsMessageProcessor.ConnectivitySettings = connectivitySettings;
            nsMessageProcessor.RegisterHandler(nsMessageHandler);
            nsMessageHandler.MessageProcessor = nsMessageProcessor;
            nsMessageHandler.Credentials = credentials;
            nsMessageHandler.ConnectivitySettings = connectivitySettings;
            nsMessageProcessor.Connect();
        }

        /// <summary>
        /// Disconnect from the messenger network.
        /// </summary>
        public virtual void Disconnect()
        {
            if (nsMessageProcessor.Connected)
            {
                if (nsMessageHandler != null)
                    nsMessageHandler.Owner.SetStatus(PresenceStatus.Offline);

                nsMessageProcessor.Disconnect();
            }
        }

        /// <summary>
        /// Creates a conversation.
        /// </summary>
        /// <remarks>
        /// This method will fire the <see cref="ConversationCreated"/> event. The initiator object of the created switchboard will be <b>this</b> messenger object.
        /// </remarks>
        /// <returns></returns>
        public Conversation CreateConversation()
        {
            Conversation conversation = new Conversation(this);
            OnConversationCreated(conversation, this);
            return conversation;
        }

        #endregion

        #endregion

        #region Protected

        /// <summary>
        /// Fires the ConversationCreated event.
        /// </summary>
        /// <param name="conversation"></param>
        /// <param name="initiator"></param>
        protected virtual void OnConversationCreated(Conversation conversation, object initiator)
        {
            if (ConversationCreated != null)
                ConversationCreated(this, new ConversationCreatedEventArgs(conversation, initiator));
        }

        #endregion

    }
};
