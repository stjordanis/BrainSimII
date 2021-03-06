﻿//
// Copyright (c) Charles Simon. All rights reserved.  
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//  

using System.Collections.Generic;
using System.Xml.Serialization;

namespace BrainSimulator
{
    public class Neuron : NeuronPartial
    {
        public string label = "";
        public List<Synapse> synapses = new List<Synapse>();
        public List<Synapse> synapsesFrom = new List<Synapse>();
        NeuronArray ownerArray = MainWindow.theNeuronArray;

        //this is only used in NeuronView but is here so you can add the tooltip when you add a neuron type and 
        //the tooltip will automatically appear in the neuron type selector combobox
        public static string[] modelToolTip = { "Integrate & Fire",
            "RGB value (no processing)",
            "Float value (no procesing",
            "Leaky Integrate & Fire",
            "Fires at random intervals"
        };

        public NeuronArray Owner { set => ownerArray = value; }

        //IMPORTANT:
        //Lastcharge is a stable readable value of the output of a neuron
        //CurrentCharge is an internal accumulation variable which must be set for the engine to act on a neuron's value
        //"Color" neurons and "FloatValue" neurons are special in that last and current should always be the same.langu
        public float CurrentCharge
        {
            get { return (float)lastCharge; }
            set { currentCharge = value; }
        }

        //get/set last charge. Setting also sets current charge

        //TODO: change this to SetValue
        public float LastCharge { get { return (float)lastCharge; } set { lastCharge = value; } }// Update(); } }

        //get/set last charge as raw integer Used by COLOR nueurons
        public int LastChargeInt { get { return (int)lastCharge; } set { lastCharge = value; Update(); } }
        public void SetValueInt(int value) { LastChargeInt = value; Update(); }

        public List<Synapse> Synapses { get => synapses; }
        [XmlIgnore]
        public List<Synapse> SynapsesFrom { get => synapsesFrom; }

        public long LastFired { get => lastFired; }

        public bool Fired() { return (LastCharge >= 1); }
        //public void SetValue(float value) { lastCharge = value; currentCharge = value; Update(); }
        public void SetValue(float value)
        {
            currentCharge = value;
            if (model == modelType.FloatValue)
                lastCharge = value;
            Update();
        }

        public enum modelType { Std, Color, FloatValue, LIF, Random };

        public int Id { get => id; set => id = value; }
        public string Label { get => label; set { label = value; Update(); } }

        //Used by LIF, Random neurons
        public float LeakRate { get => leakRate; set { leakRate = value; Update(); } }

        public modelType Model { get => (Neuron.modelType)model; set { model = (modelType)value; Update(); } }

        public void Update()
        {
            ownerArray.SetCompleteNeuron(this);
        }
        public Neuron()
        {
            if (ownerArray == null)
                ownerArray = MainWindow.myClipBoard;
        }

        //a neuron is defined as in use if it has any synapses connected from/to it or it has a label
        public bool InUse()
        {
            return ((Synapses != null && Synapses.Count != 0) || (SynapsesFrom != null && SynapsesFrom.Count != 0) || Label != "");
        }

        public void Reset()
        {
            Label = "";
            model = modelType.Std;
            SetValue(0);
        }

        public void AddSynapse(int targetNeuron, float weight, bool isHebbian)
        {
            ownerArray.AddSynapse(Id, targetNeuron, weight, isHebbian, false);
        }
        public void AddSynapse(int targetNeuron, float weight)
        {
            ownerArray.AddSynapse(Id, targetNeuron, weight, false, false);
        }
        //TODO...eliminated this
        public void AddSynapse(int targetNeuron, float weight, NeuronArray theNeuronArray, bool addUndoInfo = false)
        {
            ownerArray.AddSynapse(Id, targetNeuron, weight, false, false);
            if (addUndoInfo)
            {
                MainWindow.theNeuronArray.AddSynapseUndo(Id, targetNeuron, weight, true);
            }
        }
        public void DeleteAllSynapes()
        {
            foreach (Synapse s in Synapses)
                DeleteSynapse(s.targetNeuron);
            Synapses.Clear();
            foreach (Synapse s in synapsesFrom)
            {
                ownerArray.DeleteSynapse(s.targetNeuron, id);
            }
            synapsesFrom.Clear();
        }

        public override string ToString()
        {
            return "n:" + Id;
        }
        public void DeleteSynapse(int targetNeuron)
        {
            ownerArray.DeleteSynapse(Id, targetNeuron);
        }

        public Synapse FindSynapse(int targetNeuron)
        {
            if (Synapses == null) return null;
            for (int i = 0; i < Synapses.Count; i++)
            {
                if (((Synapse)Synapses[i]).TargetNeuron == targetNeuron)
                    return (Synapse)Synapses[i];
            }
            return null;
        }
        public Synapse FindSynapseFrom(int fromNeuron)
        {
            if (SynapsesFrom == null) return null;
            for (int i = 0; i < SynapsesFrom.Count; i++)
            {
                if (((Synapse)SynapsesFrom[i]).TargetNeuron == fromNeuron)
                    return (Synapse)SynapsesFrom[i];
            }
            return null;
        }

        public Neuron Clone()
        {
            Neuron n = (Neuron)this.MemberwiseClone();
            n.synapses = new List<Synapse>();
            n.synapsesFrom = new List<Synapse>(); ;
            return n;
        }
        //copy this content to n
        public void Copy(Neuron n)
        {
            n.label = this.label;
            n.lastCharge = this.lastCharge;
            n.currentCharge = this.currentCharge;
            n.LeakRate = this.LeakRate;
            n.model = this.model;
            n.synapsesFrom = new List<Synapse>(); ;
        }
        public void Clear()
        {
            label = "";
            currentCharge = 0;
            lastCharge = 0;
            model = modelType.Std;
            LeakRate = 0.1f;
            DeleteAllSynapes();
            MainWindow.theNeuronArray.SetCompleteNeuron(this);
            synapses = new List<Synapse>();
            synapsesFrom = new List<Synapse>();
        }
    }
}
