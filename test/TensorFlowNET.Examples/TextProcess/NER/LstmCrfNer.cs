﻿using NumSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Tensorflow;
using Tensorflow.Estimator;
using TensorFlowNET.Examples.Utility;
using static Tensorflow.Python;
using static TensorFlowNET.Examples.DataHelpers;

namespace TensorFlowNET.Examples.Text.NER
{
    /// <summary>
    /// A NER model using Tensorflow (LSTM + CRF + chars embeddings).
    /// State-of-the-art performance (F1 score between 90 and 91).
    /// 
    /// https://github.com/guillaumegenthial/sequence_tagging
    /// </summary>
    public class LstmCrfNer : IExample
    {
        public int Priority => 14;

        public bool Enabled { get; set; } = true;
        public bool ImportGraph { get; set; } = true;

        public string Name => "LSTM + CRF NER";

        HyperParams hp;
        
        int nwords, nchars, ntags;
        CoNLLDataset dev, train;

        Tensor word_ids_tensor;
        Tensor sequence_lengths_tensor;
        Tensor char_ids_tensor;
        Tensor word_lengths_tensor;
        Tensor labels_tensor;
        Tensor dropout_tensor;
        Tensor lr_tensor;

        public bool Run()
        {
            PrepareData();
            var graph = tf.Graph().as_default();

            tf.train.import_meta_graph("graph/lstm_crf_ner.meta");

            word_ids_tensor = graph.OperationByName("word_ids");
            sequence_lengths_tensor = graph.OperationByName("sequence_lengths");
            char_ids_tensor = graph.OperationByName("char_ids");
            word_lengths_tensor = graph.OperationByName("word_lengths");
            labels_tensor = graph.OperationByName("labels");
            dropout_tensor = graph.OperationByName("dropout");
            lr_tensor = graph.OperationByName("lr");

            var init = tf.global_variables_initializer();

            with(tf.Session(), sess =>
            {
                sess.run(init);

                foreach (var epoch in range(hp.epochs))
                {
                    print($"Epoch {epoch + 1} out of {hp.epochs}");
                    run_epoch(train, dev, epoch);
                }

            });

            return true;
        }

        private void run_epoch(CoNLLDataset train, CoNLLDataset dev, int epoch)
        {
            int i = 0;
            // iterate over dataset
            var batches = minibatches(train, hp.batch_size);
            foreach (var(words, labels) in batches)
            {
                get_feed_dict(words, labels, hp.lr, hp.dropout);
            }
        }

        private IEnumerable<((int[][], int[])[], int[][])> minibatches(CoNLLDataset data, int minibatch_size)
        {
            var x_batch = new List<(int[][], int[])>();
            var y_batch = new List<int[]>();
            foreach(var (x, y) in data.GetItems())
            {
                if (len(y_batch) == minibatch_size)
                {
                    yield return (x_batch.ToArray(), y_batch.ToArray());
                    x_batch.Clear();
                    y_batch.Clear();
                }

                var x3 = (x.Select(x1 => x1.Item1).ToArray(), x.Select(x2 => x2.Item2).ToArray());
                x_batch.Add(x3);
                y_batch.Add(y);
            }

            if (len(y_batch) > 0)
                yield return (x_batch.ToArray(), y_batch.ToArray());
        }

        /// <summary>
        /// Given some data, pad it and build a feed dictionary
        /// </summary>
        /// <param name="words">
        /// list of sentences. A sentence is a list of ids of a list of
        /// words. A word is a list of ids
        /// </param>
        /// <param name="labels">list of ids</param>
        /// <param name="lr">learning rate</param>
        /// <param name="dropout">keep prob</param>
        private FeedItem[] get_feed_dict((int[][], int[])[] words, int[][] labels, float lr = 0f, float dropout = 0f)
        {
            int[] sequence_lengths;
            int[][] word_lengths;
            int[][] word_ids;
            int[][][] char_ids;

            if (true) // use_chars
            {
                (char_ids, word_ids) = (words.Select(x => x.Item1).ToArray(), words.Select(x => x.Item2).ToArray());
                (word_ids, sequence_lengths) = pad_sequences(word_ids, pad_tok: 0);
                (char_ids, word_lengths) = pad_sequences(char_ids, pad_tok: 0);
            }

            // build feed dictionary
            var feeds = new List<FeedItem>();
            feeds.Add(new FeedItem(word_ids_tensor, np.array(word_ids)));
            feeds.Add(new FeedItem(sequence_lengths_tensor, np.array(sequence_lengths)));
            
            if(true) // use_chars
            {
                feeds.Add(new FeedItem(char_ids_tensor, np.array(char_ids)));
                feeds.Add(new FeedItem(word_lengths_tensor, np.array(word_lengths)));
            }

            throw new NotImplementedException("get_feed_dict");
        }

        public void PrepareData()
        {
            hp = new HyperParams("LstmCrfNer")
            {
                epochs = 15,
                dropout = 0.5f,
                batch_size = 20,
                lr_method = "adam",
                lr = 0.001f,
                lr_decay = 0.9f,
                clip = false,
                epoch_no_imprv = 3,
                hidden_size_char = 100,
                hidden_size_lstm = 300
            };
            hp.filepath_dev = hp.filepath_test = hp.filepath_train = Path.Combine(hp.data_root_dir, "test.txt");

            // Loads vocabulary, processing functions and embeddings
            hp.filepath_words = Path.Combine(hp.data_root_dir, "words.txt");
            hp.filepath_tags = Path.Combine(hp.data_root_dir, "tags.txt");
            hp.filepath_chars = Path.Combine(hp.data_root_dir, "chars.txt");

            // 1. vocabulary
            /*vocab_tags = load_vocab(hp.filepath_tags);
            

            nwords = vocab_words.Count;
            nchars = vocab_chars.Count;
            ntags = vocab_tags.Count;*/

            // 2. get processing functions that map str -> id
            dev = new CoNLLDataset(hp.filepath_dev, hp);
            train = new CoNLLDataset(hp.filepath_train, hp);
        }
    }
}
