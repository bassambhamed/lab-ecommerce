START TRANSACTION;
ALTER TABLE "Products" ADD "Category" character varying(100) NOT NULL DEFAULT 'Uncategorized';

UPDATE "Products" SET "Category" = 'Peripherals'
WHERE "Id" = 1;

UPDATE "Products" SET "Category" = 'Peripherals'
WHERE "Id" = 2;

UPDATE "Products" SET "Category" = 'Displays'
WHERE "Id" = 3;

UPDATE "Products" SET "Category" = 'Accessories'
WHERE "Id" = 4;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261108181551_AddProductCategory', '10.0.9');

COMMIT;

