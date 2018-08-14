# ServiceControl performance characteristics

## Environment

Azure VM, 16 vCPU, 64 GB RAM, 2x7500 IOPS striped disk

## Results

### Find bottlenecks

Tests were conducted using empty messages with a one-minute retention policy

 - MSMQ -> **165 msg/s**
 - ASB -> **155 msg/s**
 - SQL -> **160 msg/s**
 - ASQ -> **160 msg/s**
 - RabbitMQ -> **152 msg/s**
 
The results show that the throughput is limited by the database write speed, not the throughput of the transport. The same tests for V2 and ASB showed **70 msg/s** throughput provide that V2 ASB was limited by the transport speed, not by the disk.
 
### Investigate the impact of message size on ingestion and eviction throughput
 
Tests were conducted using messages of different sizes with 5-minute retention. RabbitMQ was used as a transport as it is known for high throughput.
 
 - 0 KB -> **152 msg/s**
 - 13 KB -> **140 msg/s**
 - 20 KB -> **130 msg/s**
 - 40 KB -> **102 msg/s**
 - 66 KB (fits both in the document and in the body storage) -> **80 msg/s , 4 GB database, 22 GB RAM after processing 450K messages, cleanup cost ~4 ms/message**
 - 93 KB (fits in body storage only) ->**107 msg/s, 12 GB RAM after processing 550K messages, cleanup cost ~2 ms/message**
 - 133 KB (too big to store) -> **150 msg/s, cleanup cost ~1 ms/message**
 
The tests show that storing the body inside a document impacts significantly the memory usage. The purpose of storing the body there is to allow a full-text search. The performance characteristics of processing small and very large (over 133 KB) messages are the same because for the latter, not the body is stored (neither as an attachment nor inside the document).

Observation of memory consumption suggests that the memory is allocated not when storing the messages but when evicting them. Changing the cleanup mechanism not to use the message metadata seems to not affect the memory allocations.

It is worth notice that the cost of cleanup seems to grow significantly with the message size with its maximum of ~4 ms/message at around 60-70 KB where the body is stored both in the document and as an attachment. 
 
###  Investigate the impact of audit retention
 
Tests were conducted using messages of decreasing sizes (as the database would be too extensive for the most extended retention periods). The messages sizes were kept under 80 KB to make sure the body is also stored as an attachment
 
 - 5 minutes -> **80 msg/s , 4 GB database, 22 GB RAM after processing 450K messages, cleanup cost ~4 ms/message**
 - 30 minutes -> **65 msg/s, 20 GB database, up to 60 GB of RAM**
 - 6 hours -> **65 msg/s, 300 GB database, cleanup cost ~6 ms/message**
 - 3 days (10 KB) -> **120 msg/s, 1 TB database, cleanup cost ~8 ms/message**

The cost of cleanup grows with the database size (in addition to growing in a function of message size, as noted in the previous section). The baseline cost is ~1ms for a small message and a small database. It grows to 8-10 ms for the same message size but a 1 TB database.

The last 3 day retention period run showed that the ingestion and cleanup throughputs do not automatically balance. For three days the database size was at 1.09 TB but then started growing up to 1.2 TB after another 2 days. This proves that to keep DB size in check, and a feedback mechanism is needed to slow down the ingestion if the cleanup can't cope up.
