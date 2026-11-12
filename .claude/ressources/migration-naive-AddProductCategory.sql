START TRANSACTION;
ALTER TABLE "Products" ADD "Category" character varying(100) NOT NULL DEFAULT '';

UPDATE "Products" SET "Category" = ''
WHERE "Id" = 1;

UPDATE "Products" SET "Category" = ''
WHERE "Id" = 2;

UPDATE "Products" SET "Category" = ''
WHERE "Id" = 3;

UPDATE "Products" SET "Category" = ''
WHERE "Id" = 4;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261108181508_AddProductCategory', '10.0.9');

COMMIT;

