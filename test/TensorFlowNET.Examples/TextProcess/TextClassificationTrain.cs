﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NumSharp;
using Tensorflow;
using Tensorflow.Keras.Engine;
using TensorFlowNET.Examples.Text.cnn_models;
using TensorFlowNET.Examples.TextClassification;
using TensorFlowNET.Examples.Utility;
using static Tensorflow.Python;

namespace TensorFlowNET.Examples.CnnTextClassification
{
    /// <summary>
    /// https://github.com/dongjun-Lee/text-classification-models-tf
    /// </summary>
    public class TextClassificationTrain : IExample
    {
        public int Priority => 100;
        public bool Enabled { get; set; } = false;
        public string Name => "Text Classification";
        public int? DataLimit = null;
        public bool ImportGraph { get; set; } = true;

        private string dataDir = "text_classification";
        private string dataFileName = "dbpedia_csv.tar.gz";

        public string model_name = "vd_cnn"; // word_cnn | char_cnn | vd_cnn | word_rnn | att_rnn | rcnn

        private const int CHAR_MAX_LEN = 1014;
        private const int NUM_CLASS = 2;
        private const int BATCH_SIZE = 64;
        private const int NUM_EPOCHS = 10;
        protected float loss_value = 0;

        public bool Run()
        {
            PrepareData();
            return with(tf.Session(), sess =>
            {
                if (ImportGraph)
                    return RunWithImportedGraph(sess);
                else
                    return RunWithBuiltGraph(sess);
            });
        }

        protected virtual bool RunWithImportedGraph(Session sess)
        {
            var graph = tf.Graph().as_default();
            Console.WriteLine("Building dataset...");
            var (x, y, alphabet_size) = DataHelpers.build_char_dataset("train", model_name, CHAR_MAX_LEN, DataLimit);

            var (train_x, valid_x, train_y, valid_y) = train_test_split(x, y, test_size: 0.15f);

            var meta_file = model_name + "_untrained.meta";
            tf.train.import_meta_graph(Path.Join("graph", meta_file));

            //sess.run(tf.global_variables_initializer()); // not necessary here, has already been done before meta graph export

            var train_batches = batch_iter(train_x, train_y, BATCH_SIZE, NUM_EPOCHS);
            var num_batches_per_epoch = (len(train_x) - 1); // BATCH_SIZE + 1
            double max_accuracy = 0;

            Tensor is_training = graph.get_operation_by_name("is_training");
            Tensor model_x = graph.get_operation_by_name("x");
            Tensor model_y = graph.get_operation_by_name("y");
            Tensor loss = graph.get_operation_by_name("Variable");
            Tensor accuracy = graph.get_operation_by_name("accuracy/accuracy");

            foreach (var (x_batch, y_batch) in train_batches)
            {
                var train_feed_dict = new Hashtable
                {
                    [model_x] = x_batch,
                    [model_y] = y_batch,
                    [is_training] = true,
                };

                //_, step, loss = sess.run([model.optimizer, model.global_step, model.loss], feed_dict = train_feed_dict)
            }

            return false;
        }

        protected virtual bool RunWithBuiltGraph(Session session)
        {
            Console.WriteLine("Building dataset...");
            var (x, y, alphabet_size) = DataHelpers.build_char_dataset("train", model_name, CHAR_MAX_LEN, DataLimit);

            var (train_x, valid_x, train_y, valid_y) = train_test_split(x, y, test_size: 0.15f);

            ITextClassificationModel model = null;
            switch (model_name) // word_cnn | char_cnn | vd_cnn | word_rnn | att_rnn | rcnn
            {
                case "word_cnn":
                case "char_cnn":
                case "word_rnn":
                case "att_rnn":
                case "rcnn":
                    throw new NotImplementedException();
                    break;
                case "vd_cnn":
                    model=new VdCnn(alphabet_size, CHAR_MAX_LEN, NUM_CLASS);
                    break;
            }
            // todo train the model
            return false;
        }

        private (int[][], int[][], int[], int[]) train_test_split(int[][] x, int[] y, float test_size = 0.3f)
        {
            int len = x.Length;
            int classes = y.Distinct().Count();
            int samples = len / classes;
            int train_size = int.Parse((samples * (1 - test_size)).ToString());

            var train_x = new List<int[]>();
            var valid_x = new List<int[]>();
            var train_y = new List<int>();
            var valid_y = new List<int>();

            for (int i = 0; i < classes; i++)
            {
                for (int j = 0; j < samples; j++)
                {
                    int idx = i * samples + j;
                    if (idx < train_size + samples * i)
                    {
                        train_x.Add(x[idx]);
                        train_y.Add(y[idx]);
                    }
                    else
                    {
                        valid_x.Add(x[idx]);
                        valid_y.Add(y[idx]);
                    }
                }
            }

            return (train_x.ToArray(), valid_x.ToArray(), train_y.ToArray(), valid_y.ToArray());
        }

        private IEnumerable<(NDArray, NDArray)> batch_iter(int[][] raw_inputs, int[] raw_outputs, int batch_size, int num_epochs)
        {
            var inputs = np.array(raw_inputs);
            var outputs = np.array(raw_outputs);

            var num_batches_per_epoch = (len(inputs) - 1); // batch_size + 1
            foreach (var epoch in range(num_epochs))
            {
                foreach (var batch_num in range(num_batches_per_epoch))
                {
                    var start_index = batch_num * batch_size;
                    var end_index = Math.Min((batch_num + 1) * batch_size, len(inputs));
                    yield return (inputs[$"{start_index}:{end_index}"], outputs[$"{start_index}:{end_index}"]);
                }
            }
        }

        public void PrepareData()
        {
            string url = "https://github.com/le-scientifique/torchDatasets/raw/master/dbpedia_csv.tar.gz";
            Web.Download(url, dataDir, dataFileName);
            Compress.ExtractTGZ(Path.Join(dataDir, dataFileName), dataDir);

            if (ImportGraph)
            {
                // download graph meta data
                var meta_file = model_name + "_untrained.meta";
                url = "https://raw.githubusercontent.com/SciSharp/TensorFlow.NET/master/graph/" + meta_file;
                Web.Download(url, "graph", meta_file);
            }
        }
    }
}
