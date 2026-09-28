-- Optional local demo only. Run manually against a disposable SQL Server database.
CREATE TABLE dbo.erp_customer(customer_code nvarchar(20) NOT NULL PRIMARY KEY,customer_name nvarchar(200) NOT NULL,email_addr nvarchar(320) NULL);
CREATE TABLE dbo.erp_product(product_code nvarchar(20) NOT NULL PRIMARY KEY,product_name nvarchar(200) NOT NULL,unit_price decimal(18,2) NOT NULL);
CREATE TABLE dbo.erp_sales_invoice(doc_no nvarchar(30) NOT NULL PRIMARY KEY,customer_code nvarchar(20) NOT NULL,doc_date date NOT NULL,due_date date NULL,net_total decimal(18,2) NOT NULL,status nvarchar(30) NOT NULL);
CREATE TABLE dbo.erp_sales_invoice_item(id int IDENTITY PRIMARY KEY,doc_no nvarchar(30) NOT NULL,product_code nvarchar(20) NOT NULL,quantity decimal(18,2) NOT NULL,unit_price decimal(18,2) NOT NULL);
CREATE TABLE dbo.erp_payment(id int IDENTITY PRIMARY KEY,doc_no nvarchar(30) NOT NULL,paid_amount decimal(18,2) NOT NULL,paid_date date NOT NULL);
CREATE TABLE dbo.erp_inventory(product_code nvarchar(20) NOT NULL PRIMARY KEY,qty_on_hand decimal(18,2) NOT NULL,reorder_level decimal(18,2) NOT NULL);
INSERT dbo.erp_customer VALUES('C-100','Acme Retail','buyer@acme.example'),('C-200','Northwind Demo','accounts@northwind.example');
INSERT dbo.erp_product VALUES('P-10','USB-C Dock',129.99),('P-20','Wireless Mouse',39.50);
INSERT dbo.erp_sales_invoice VALUES('EXT-1001','C-100','2026-09-01','2026-09-15',129.99,'Overdue'),('EXT-1002','C-200','2026-09-20','2026-10-20',79.00,'Issued');
INSERT dbo.erp_sales_invoice_item(doc_no,product_code,quantity,unit_price) VALUES('EXT-1001','P-10',1,129.99),('EXT-1002','P-20',2,39.50);
INSERT dbo.erp_payment(doc_no,paid_amount,paid_date) VALUES('EXT-1001',25.00,'2026-09-10');
INSERT dbo.erp_inventory VALUES('P-10',2,5),('P-20',25,10);
